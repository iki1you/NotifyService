using Abstractions.Models.Enums;

namespace RateLimiter.Configuration
{
    public sealed class RateLimitOptions
    {
        public const string SectionName = "RateLimits";

        public int DefaultMaxRps { get; set; } = 0;

        public int RedisRequestTimeoutMs { get; set; } = 100;

        public double CapacityMultiplier { get; set; } = 1.5;

        public Dictionary<string, Dictionary<string, ProviderRateLimitOptions>> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public int ResolveMaxRps(ChannelType channel, AdapterType provider)
        {
            var normalizedChannel = Normalize(channel);
            var normalizedProvider = Normalize(provider);
            if (TryGetProviderOptions(normalizedChannel, normalizedProvider, out var providerOptions)
                && providerOptions?.MaxRequestsPerSecond > 0)
            {
                return providerOptions.MaxRequestsPerSecond;
            }

            return DefaultMaxRps > 0 ? DefaultMaxRps : 0;
        }

        public int ResolveCredentialMaxRps(ChannelType channel, AdapterType provider)
        {
            var normalizedChannel = Normalize(channel);
            var normalizedProvider = Normalize(provider);
            if (TryGetProviderOptions(normalizedChannel, normalizedProvider, out var providerOptions)
                && providerOptions?.MaxRequestsPerSecondPerCredential > 0)
            {
                return providerOptions.MaxRequestsPerSecondPerCredential;
            }

            return 0;
        }

        private bool TryGetProviderOptions(
            string normalizedChannel,
            string normalizedProvider,
            out ProviderRateLimitOptions? providerOptions)
        {
            providerOptions = null;

            if (!Providers.TryGetValue(normalizedChannel, out var providerLimits) || providerLimits is null)
            {
                return false;
            }

            if (providerLimits.TryGetValue(normalizedProvider, out providerOptions))
            {
                return true;
            }

            foreach (var entry in providerLimits)
            {
                if (string.Equals(entry.Key, normalizedProvider, StringComparison.OrdinalIgnoreCase))
                {
                    providerOptions = entry.Value;
                    return true;
                }
            }

            return false;
        }

        public static string Normalize(ChannelType value)
            => value.ToString().ToLowerInvariant();

        public static string Normalize(AdapterType value)
            => value.ToString().ToLowerInvariant();
    }

    public sealed class ProviderRateLimitOptions
    {
        public int MaxRequestsPerSecond { get; set; }
        public int MaxRequestsPerSecondPerCredential { get; set; }
    }
}
