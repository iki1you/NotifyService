using Abstractions.Models.Enums;

namespace Queue.Interfaces
{
    public interface IRateLimiter
    {
        Task WaitAsync(ChannelType channel, AdapterType provider, CancellationToken ct = default);
    }
}
