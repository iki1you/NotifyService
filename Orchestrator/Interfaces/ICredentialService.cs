using Abstractions.Models.Credentials;

namespace Orchestrator.Interfaces
{
    public interface ICredentialService
    {
        Task<CredentialShortInfo> SelectCredentialAsync(long projectId, string channel);
    }
}
