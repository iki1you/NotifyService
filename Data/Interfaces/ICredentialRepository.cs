using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Entities;
using System.Text.Json;

namespace Data.Interfaces
{
    public interface ICredentialRepository : IRepository<Credential>
    {
        Task<List<CredentialShortInfo>?> GetCredentialsByProjectAndChannelAsync(long projectId, ChannelType channel);
        Task<CredentialShortInfo> AddCredentialAsync(long projectId, ChannelType channel, AdapterType adapterType, JsonDocument config);
        Task<List<long>> GetActiveCredentialIdsByAdapterTypeAsync(AdapterType adapterType, CancellationToken ct);
    }
}
