using Abstractions.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateLimiter.Configuration;
using RateLimiter.Interfaces;
using RateLimiter.Telemetry;
using StackExchange.Redis;
using System.Globalization;

namespace RateLimiter.Services
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
local acquired = 0
local tokens_before = tokens
if tokens >= 1 then
    tokens = tokens - 1
    acquired = 1
else
    wait_time = (1 - tokens) / max_rps
end

redis.call('HMSET', key, 'tokens', tokens, 'last_refill', last_refill)
local ttl = math.ceil((capacity / max_rps) * 2)
if ttl < 1 then
    ttl = 1
end
redis.call('EXPIRE', key, ttl)

return { wait_time, tokens, tokens_before, acquired }
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
            var capacity = ResolveCapacity(maxRps, options.CapacityMultiplier);
            var key = $"rate_limit:{normalizedChannel}:{normalizedProvider}";

            if (maxRps > 0)
            {
                RateLimitMetrics.SetMaxRps(normalizedChannel, normalizedProvider, maxRps);
            }

            var totalWait = TimeSpan.Zero;

            while (!ct.IsCancellationRequested)
            {
                if (maxRps <= 0)
                {
                    return;
                }

                var result = await TryAcquireWithRedisAsync(key, maxRps, capacity, options.RedisRequestTimeoutMs, ct);

                if (result is null)
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

                var tokensMetric = result.Value.TokensBefore ?? result.Value.Tokens;
                RateLimitMetrics.SetAvailableTokens(normalizedChannel, normalizedProvider, tokensMetric);

                if (result.Value.WaitSec <= 0 && result.Value.Acquired)
                {
                    if (totalWait > TimeSpan.Zero)
                    {
                        RateLimitMetrics.RecordWaitTime(totalWait.TotalMilliseconds, normalizedChannel, normalizedProvider);
                    }

                    RateLimitMetrics.IncrementAcquired(normalizedChannel, normalizedProvider);

                    return;
                }

                var waitSeconds = result.Value.WaitSec <= 0
                    ? 1d / maxRps
                    : result.Value.WaitSec;

                RateLimitMetrics.IncrementExceeded(normalizedChannel, normalizedProvider);

                var waitDelay = TimeSpan.FromSeconds(waitSeconds);
                if (waitDelay < MinDelay)
                {
                    waitDelay = MinDelay;
                }

                totalWait += waitDelay;
                await Task.Delay(waitDelay, ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        public async Task WaitAsync(
            ChannelType channel,
            AdapterType provider,
            string? credentialKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(credentialKey))
            {
                return;
            }

            var normalizedChannel = RateLimitOptions.Normalize(channel);
            var normalizedProvider = RateLimitOptions.Normalize(provider);
            var normalizedCredential = credentialKey.Trim().ToLowerInvariant();
            var options = _options.CurrentValue;

            var providerCredentialLimit = options.ResolveCredentialMaxRps(channel, provider);
            if (providerCredentialLimit <= 0)
            {
                return;
            }

            var capacity = ResolveCapacity(providerCredentialLimit, options.CapacityMultiplier);
            var key = $"rate_limit:credential:{normalizedChannel}:{normalizedProvider}:{normalizedCredential}";
            var totalWait = TimeSpan.Zero;

            RateLimitMetrics.SetMaxRps(normalizedChannel, normalizedProvider, providerCredentialLimit);

            while (!ct.IsCancellationRequested)
            {
                var result = await TryAcquireWithRedisAsync(key, providerCredentialLimit, capacity, options.RedisRequestTimeoutMs, ct);

                if (result is null)
                {
                    var fallbackDelay = TimeSpan.FromSeconds(1d / providerCredentialLimit);
                    RateLimitMetrics.IncrementFallback(normalizedChannel, normalizedProvider);

                    _logger.LogWarning(
                        "Rate limiter fallback is active for credential {Channel}:{Provider}:{Credential}. Applying local delay {DelayMs} ms",
                        normalizedChannel,
                        normalizedProvider,
                        normalizedCredential,
                        fallbackDelay.TotalMilliseconds);

                    totalWait += fallbackDelay;
                    await Task.Delay(fallbackDelay, ct);
                    continue;
                }

                var tokensMetric = result.Value.TokensBefore ?? result.Value.Tokens;
                RateLimitMetrics.SetAvailableTokens(normalizedChannel, normalizedProvider, tokensMetric, normalizedCredential);

                if (result.Value.WaitSec <= 0 && result.Value.Acquired)
                {
                    if (totalWait > TimeSpan.Zero)
                    {
                        RateLimitMetrics.RecordWaitTime(totalWait.TotalMilliseconds, normalizedChannel, normalizedProvider);
                    }

                    RateLimitMetrics.IncrementAcquired(normalizedChannel, normalizedProvider, normalizedCredential);
                    return;
                }

                var waitSeconds = result.Value.WaitSec <= 0
                    ? 1d / providerCredentialLimit
                    : result.Value.WaitSec;

                RateLimitMetrics.IncrementExceeded(normalizedChannel, normalizedProvider);

                var waitDelay = TimeSpan.FromSeconds(waitSeconds);
                if (waitDelay < MinDelay)
                {
                    waitDelay = MinDelay;
                }

                totalWait += waitDelay;
                await Task.Delay(waitDelay, ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        private static double ResolveCapacity(int maxRps, double capacityMultiplier)
        {
            return Math.Max(1d, maxRps * Math.Max(1d, capacityMultiplier));
        }

        private async Task<RateLimitAcquireResult?> TryAcquireWithRedisAsync(
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
                    return null;
                }

                if (result.Type == ResultType.MultiBulk)
                {
                    var values = (RedisValue[])result;
                    if (values.Length >= 3
                        && double.TryParse(values[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var waitSec)
                        && double.TryParse(values[1].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tokens))
                    {
                        double? tokensBefore = null;
                        var acquired = false;

                        if (values.Length >= 4
                            && double.TryParse(values[2].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedTokensBefore))
                        {
                            tokensBefore = parsedTokensBefore;
                        }

                        if (values.Length >= 4
                            && int.TryParse(values[3].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var acquiredInt))
                        {
                            acquired = acquiredInt == 1;
                        }
                        else if (values.Length == 3
                            && int.TryParse(values[2].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyAcquired))
                        {
                            acquired = legacyAcquired == 1;
                        }

                        return new RateLimitAcquireResult(waitSec, tokens, tokensBefore, acquired);
                    }
                }

                return null;
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

        private readonly record struct RateLimitAcquireResult(double WaitSec, double Tokens, double? TokensBefore, bool Acquired);
    }
}
