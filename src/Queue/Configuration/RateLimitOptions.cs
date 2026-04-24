using Abstractions.Models.Enums;

namespace Queue.Configuration
{
    public sealed class RateLimitOptions
    {
        public const string SectionName = "RateLimits";

        public int DefaultMaxRps { get; set; } = 5;

        public int RedisRequestTimeoutMs { get; set; } = 100;

        public double CapacityMultiplier { get; set; } = 1.5;

        public Dictionary<string, ProviderRateLimitOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public int ResolveMaxRps(ChannelType channel, AdapterType provider)
        {
            var normalizedChannel = Normalize(channel);
            var normalizedProvider = Normalize(provider);
            var key = $"{normalizedChannel}:{normalizedProvider}";

            if (Providers.TryGetValue(key, out var providerOptions) && providerOptions.MaxRequestsPerSecond > 0)
            {
                return providerOptions.MaxRequestsPerSecond;
            }

            return Math.Max(1, DefaultMaxRps);
        }

        public static string Normalize(ChannelType value)
            => value.ToString().ToLowerInvariant();

        public static string Normalize(AdapterType value)
            => value.ToString().ToLowerInvariant();
    }

    public sealed class ProviderRateLimitOptions
    {
        public int MaxRequestsPerSecond { get; set; }
    }
}
