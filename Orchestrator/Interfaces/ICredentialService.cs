using Abstractions.Models;
using Abstractions.Models.Enums;
using System.Text.Json;

namespace Orchestrator.Interfaces
{
    public interface ICredentialService
    {
        Task<CredentialShortInfo> SelectCredentialAsync(long projectId, string channel);
        Task<CredentialShortInfo> CreateCredentialAsync(long projectId, string channel, AdapterType adapterType, JsonDocument config);
    }
}
