using Abstractions.Models;
using Data.Entities;

namespace Data.Interfaces
{
    public interface ICredentialRepository : IRepository<Credential>
    {
        Task<List<CredentialShortInfo>?> GetCredentialsByProjectAndChannelAsync(long projectId, string channel);
        Task<CredentialShortInfo> AddCredentialAsync(long projectId, string channel, long adapterType, string config);
    }
}
