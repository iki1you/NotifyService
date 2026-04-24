using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Interfaces;
using System.Text.Json;
using Adapters.GreenAPI.Models;

namespace Adapters.Services
{
    public class CredentialService
    {
        private readonly ICredentialRepository _credentialRepository;

        public CredentialService(ICredentialRepository credentialRepository)
        {
            _credentialRepository = credentialRepository;
        }

        public Task<OperationResult<GreenApiOptions>> GetCredential(long credentialId)
        {
            return GetCredential<GreenApiOptions>(credentialId);
        }

        public async Task<OperationResult<TCredentialOptions>> GetCredential<TCredentialOptions>(long credentialId)
        {
            var credential = await _credentialRepository.GetByIdAsync(credentialId);

            if (credential == null)
            {
                return Error.NotFound($"Credential with ID {credentialId} not found");
            }

            if (!credential.IsActive)
            {
                return Error.BadRequest($"Credential with ID {credentialId} is not active");
            }

            try
            {
                var options = JsonSerializer.Deserialize<TCredentialOptions>(credential.Config);

                if (options == null)
                {
                    return Error.BadRequest($"Failed to deserialize credential config for ID {credentialId}");
                }

                return options;
            }
            catch (JsonException ex)
            {
                return Error.BadRequest($"Invalid credential config format for ID {credentialId}. Config value: '{credential.Config}'. Error: {ex.Message}");
            }
        }
    }
}
