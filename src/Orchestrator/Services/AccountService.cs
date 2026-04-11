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
                throw new UnauthorizedAccessException("HttpContext is null");
            }

            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("User is not authenticated");
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            var projectIdClaim = httpContext.User.FindFirst("ProjectId");

            if (projectIdClaim != null && long.TryParse(projectIdClaim.Value, out var projectId))
            {
                _logger.LogDebug("Retrieved ProjectId={ProjectId} from claims", projectId);
                return Task.FromResult(projectId);
            }

            _logger.LogWarning("No valid ProjectId claim found");
            throw new UnauthorizedAccessException("No valid ProjectId claim found");
        }
    }
}
