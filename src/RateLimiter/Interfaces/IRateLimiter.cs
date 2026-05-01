using Abstractions.Models.Enums;

namespace RateLimiter.Interfaces
{
    public interface IRateLimiter
    {
        Task WaitAsync(ChannelType channel, AdapterType provider, CancellationToken ct = default);
        Task WaitAsync(ChannelType channel, AdapterType provider, string? credentialKey, CancellationToken ct = default);
    }
}
