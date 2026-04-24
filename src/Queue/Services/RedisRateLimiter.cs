using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abstractions.Models.Enums;
using Queue.Configuration;
using Queue.Interfaces;
using Queue.Telemetry;
using StackExchange.Redis;
using System.Globalization;

namespace Queue.Services
{
    public sealed class RedisRateLimiter : IRateLimiter
    {
        private const string TokenBucketLuaScript = @"
local key = KEYS[1]
local now = tonumber(ARGV[1])
local max_rps = tonumber(ARGV[2])
local capacity = tonumber(ARGV[3])

local values = redis.call('HMGET', key, 'tokens', 'last_refill')
local tokens = tonumber(values[1])
local last_refill = tonumber(values[2])

if tokens == nil then
    tokens = capacity
end

if last_refill == nil then
    last_refill = now
end

local elapsed = now - last_refill
if elapsed < 0 then
    elapsed = 0
end

local delta = elapsed * max_rps
if delta > 0 then
    tokens = math.min(capacity, tokens + delta)
    last_refill = now
end

local wait_time = 0
if tokens >= 1 then
    tokens = tokens - 1
else
    wait_time = (1 - tokens) / max_rps
end

redis.call('HMSET', key, 'tokens', tokens, 'last_refill', last_refill)
local ttl = math.ceil((capacity / max_rps) * 2)
if ttl < 1 then
    ttl = 1
end
redis.call('EXPIRE', key, ttl)

return wait_time
";

        private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(1);

        private readonly ILogger<RedisRateLimiter> _logger;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IOptionsMonitor<RateLimitOptions> _options;

        public RedisRateLimiter(
            ILogger<RedisRateLimiter> logger,
            IConnectionMultiplexer connectionMultiplexer,
            IOptionsMonitor<RateLimitOptions> options)
        {
            _logger = logger;
            _connectionMultiplexer = connectionMultiplexer;
            _options = options;
        }

        public async Task WaitAsync(ChannelType channel, AdapterType provider, CancellationToken ct = default)
        {
            var normalizedChannel = RateLimitOptions.Normalize(channel);
            var normalizedProvider = RateLimitOptions.Normalize(provider);
            var options = _options.CurrentValue;

            var maxRps = options.ResolveMaxRps(channel, provider);
            var capacity = Math.Max(1d, maxRps * Math.Max(1d, options.CapacityMultiplier));
            var key = $"rate_limit:{normalizedChannel}:{normalizedProvider}";

            var totalWait = TimeSpan.Zero;

            while (!ct.IsCancellationRequested)
            {
                var waitSec = await TryAcquireWithRedisAsync(key, maxRps, capacity, options.RedisRequestTimeoutMs, ct);

                if (waitSec is null)
                {
                    var fallbackDelay = TimeSpan.FromSeconds(1d / maxRps);
                    RateLimitMetrics.IncrementFallback(normalizedChannel, normalizedProvider);

                    _logger.LogWarning(
                        "Rate limiter fallback is active for {Channel}:{Provider}. Applying local delay {DelayMs} ms",
                        normalizedChannel,
                        normalizedProvider,
                        fallbackDelay.TotalMilliseconds);

                    totalWait += fallbackDelay;
                    await Task.Delay(fallbackDelay, ct);
                    continue;
                }

                if (waitSec.Value <= 0)
                {
                    if (totalWait > TimeSpan.Zero)
                    {
                        RateLimitMetrics.RecordWaitTime(totalWait.TotalMilliseconds, normalizedChannel, normalizedProvider);
                    }

                    return;
                }

                RateLimitMetrics.IncrementExceeded(normalizedChannel, normalizedProvider);

                var waitDelay = TimeSpan.FromSeconds(waitSec.Value);
                if (waitDelay < MinDelay)
                {
                    waitDelay = MinDelay;
                }

                totalWait += waitDelay;
                await Task.Delay(waitDelay, ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        private async Task<double?> TryAcquireWithRedisAsync(
            string key,
            int maxRps,
            double capacity,
            int requestTimeoutMs,
            CancellationToken ct)
        {
            try
            {
                var db = _connectionMultiplexer.GetDatabase();
                var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;

                var evalTask = db.ScriptEvaluateAsync(
                    TokenBucketLuaScript,
                    new RedisKey[] { key },
                    new RedisValue[]
                    {
                        nowSeconds.ToString("R", CultureInfo.InvariantCulture),
                        maxRps,
                        capacity.ToString("R", CultureInfo.InvariantCulture)
                    });

                var timeoutTask = Task.Delay(Math.Max(1, requestTimeoutMs), CancellationToken.None);
                var completed = await Task.WhenAny(evalTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    return null;
                }

                var result = await evalTask;

                if (result.IsNull)
                {
                    return 0;
                }

                return (double)result;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogWarning(ex, "Redis timeout while applying rate limit for key {RateLimitKey}", key);
                return null;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Redis connection error while applying rate limit for key {RateLimitKey}", key);
                return null;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout while applying rate limit for key {RateLimitKey}", key);
                return null;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while applying rate limit for key {RateLimitKey}", key);
                return null;
            }
        }
    }
}
