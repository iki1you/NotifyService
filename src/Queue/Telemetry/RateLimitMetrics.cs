using System.Diagnostics.Metrics;

namespace Queue.Telemetry
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
    }
}
