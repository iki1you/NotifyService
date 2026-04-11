using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Interfaces;
using Microsoft.Extensions.Logging;
using Orchestrator.Interfaces;
using System.Text.Json;

namespace Orchestrator.Services
{
    public class CredentialService : ICredentialService
    {
        private readonly ICredentialRepository _credentialRepo;
        private readonly ILogger<CredentialService> _logger;

        public CredentialService(
            ICredentialRepository credentialRepo,
            ILogger<CredentialService> logger)
        {
            _credentialRepo = credentialRepo;
            _logger = logger;
        }

        public async Task<CredentialShortInfo> SelectCredentialAsync(long projectId, ChannelType channel)
        {
            var credentials = await _credentialRepo.GetCredentialsByProjectAndChannelAsync(projectId, channel);

            if (credentials == null || !credentials.Any())
            {
                _logger.LogWarning(
                    "No credentials found for ProjectId={ProjectId}, Channel={Channel}",
                    projectId, channel);
                return null;
            }

            var credential = credentials.First();

            _logger.LogDebug(
                "Selected credential {CredentialId} for ProjectId={ProjectId}, Channel={Channel}",
                credential.CredentialId, projectId, channel);

            return credential;
        }

        public async Task<CredentialShortInfo> CreateCredentialAsync(long projectId, ChannelType channel, AdapterType adapterType, JsonDocument config)
        {
            _logger.LogInformation(
                "Creating credential for ProjectId={ProjectId}, Channel={Channel}, AdapterType={AdapterType}",
                projectId, channel, adapterType);

            var credential = await _credentialRepo.AddCredentialAsync(projectId, channel, adapterType, config);

            _logger.LogInformation(
                "Created credential {CredentialId} for ProjectId={ProjectId}, Channel={Channel}",
                credential.CredentialId, projectId, channel);

            return credential;
        }
    }
}
