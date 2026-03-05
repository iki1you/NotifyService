using Abstractions.Models;

namespace Orchestrator.Interfaces
{
    public interface ICredentialService
    {
        Task<CredentialShortInfo> SelectCredentialAsync(long projectId, string channel);
        Task<CredentialShortInfo> CreateCredentialAsync(long projectId, string channel, long adapterType, string config);
    }
}
