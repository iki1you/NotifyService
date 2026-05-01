using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RateLimiter.Telemetry
{
    public static class RateLimitMetrics
    {
        public const string MeterName = "NotifyService.RateLimiting";

        private static readonly Meter Meter = new(MeterName);

        private static readonly Counter<long> RateLimitExceeded =
            Meter.CreateCounter<long>(
                "notify.rate_limit.exceeded",
                unit: "{event}",
                description: "Number of times a worker had to wait for rate limit");

        private static readonly Histogram<double> RateLimitWaitTimeMs =
            Meter.CreateHistogram<double>(
                "notify.rate_limit.wait_time",
                unit: "ms",
                description: "Time spent waiting for rate limit token");

        private static readonly Counter<long> RateLimitFallback =
            Meter.CreateCounter<long>(
                "notify.rate_limit.fallback",
                unit: "{event}",
                description: "Number of times fallback local rate limit was used due to Redis unavailability");

        private static readonly Counter<long> RateLimitAcquired =
            Meter.CreateCounter<long>(
                "notify.rate_limit.acquired",
                unit: "{event}",
                description: "Number of times a rate limit token was acquired");

        private static readonly ConcurrentDictionary<(string Channel, string Provider), double> MaxRpsByProvider = new();
        private static readonly ConcurrentDictionary<(string Channel, string Provider, string Credential), TokenSnapshot> AvailableTokensByScope = new();
        private static readonly TimeSpan TokenSnapshotTtl = TimeSpan.FromMinutes(5);

        private static readonly ObservableGauge<double> RateLimitMaxRps =
            Meter.CreateObservableGauge(
                "notify.rate_limit.max_rps",
                ObserveMaxRps,
                unit: "1/s",
                description: "Configured max RPS per channel/provider");

        private static readonly ObservableGauge<double> RateLimitAvailableTokens =
            Meter.CreateObservableGauge(
                "notify.rate_limit.available_tokens",
                ObserveAvailableTokens,
                unit: "{token}",
                description: "Available rate limit tokens in Redis");

        public static void IncrementExceeded(string channel, string provider)
        {
            RateLimitExceeded.Add(1,
                new KeyValuePair<string, object?>("channel", channel),
                new KeyValuePair<string, object?>("provider", provider));
        }

        public static void RecordWaitTime(double waitMs, string channel, string provider)
        {
            RateLimitWaitTimeMs.Record(waitMs,
                new KeyValuePair<string, object?>("channel", channel),
                new KeyValuePair<string, object?>("provider", provider));
        }

        public static void IncrementFallback(string channel, string provider)
        {
            RateLimitFallback.Add(1,
                new KeyValuePair<string, object?>("channel", channel),
                new KeyValuePair<string, object?>("provider", provider));
        }

        public static void IncrementAcquired(string channel, string provider, string? credential = null)
        {
            RateLimitAcquired.Add(1,
                new KeyValuePair<string, object?>("channel", channel),
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("credential", credential ?? string.Empty));
        }

        public static void SetMaxRps(string channel, string provider, double maxRps)
        {
            MaxRpsByProvider[(channel, provider)] = maxRps;
        }

        public static void SetAvailableTokens(string channel, string provider, double tokens, string? credential = null)
        {
            var normalizedCredential = credential ?? string.Empty;
            AvailableTokensByScope[(channel, provider, normalizedCredential)] = new TokenSnapshot(tokens, DateTimeOffset.UtcNow);
        }

        private static IEnumerable<Measurement<double>> ObserveMaxRps()
        {
            foreach (var entry in MaxRpsByProvider)
            {
                yield return new Measurement<double>(
                    entry.Value,
                    new KeyValuePair<string, object?>("channel", entry.Key.Channel),
                    new KeyValuePair<string, object?>("provider", entry.Key.Provider));
            }
        }

        private static IEnumerable<Measurement<double>> ObserveAvailableTokens()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in AvailableTokensByScope)
            {
                if (now - entry.Value.UpdatedAt > TokenSnapshotTtl)
                {
                    _ = AvailableTokensByScope.TryRemove(entry.Key, out _);
                    continue;
                }

                var tags = new List<KeyValuePair<string, object?>>
                {
                    new("channel", entry.Key.Channel),
                    new("provider", entry.Key.Provider)
                };

                if (!string.IsNullOrWhiteSpace(entry.Key.Credential))
                {
                    tags.Add(new KeyValuePair<string, object?>("credential", entry.Key.Credential));
                }

                yield return new Measurement<double>(entry.Value.Tokens, tags);
            }
        }

        private readonly record struct TokenSnapshot(double Tokens, DateTimeOffset UpdatedAt);
    }
}
