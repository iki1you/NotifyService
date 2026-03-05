using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Interfaces;

namespace Orchestrator.Services
{
    public class AccountService : IAccountService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<AccountService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Task<long> GetProjectIdFromAuthorized()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null");
                return Task.FromResult(1L);
            }

            var projectIdClaim = httpContext.User.FindFirst("ProjectId");
            
            if (projectIdClaim != null && long.TryParse(projectIdClaim.Value, out var projectId))
            {
                _logger.LogDebug("Retrieved ProjectId={ProjectId} from claims", projectId);
                return Task.FromResult(projectId);
            }

            _logger.LogDebug("No ProjectId claim found, using default ProjectId=1");
            return Task.FromResult(1L);
        }
    }
}
