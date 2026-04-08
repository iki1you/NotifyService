using Abstractions.Models;
using Abstractions.Models.Enums;
using System.Text.Json;

namespace Orchestrator.Interfaces
{
    public interface ICredentialService
    {
        Task<CredentialShortInfo> SelectCredentialAsync(long projectId, ChannelType channel);
        Task<CredentialShortInfo> CreateCredentialAsync(long projectId, ChannelType channel, AdapterType adapterType, JsonDocument config);
    }
}
