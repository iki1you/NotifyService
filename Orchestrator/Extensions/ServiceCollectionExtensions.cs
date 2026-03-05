using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Interfaces;
using Orchestrator.Services;

namespace Orchestrator.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOrchestratorServices(this IServiceCollection services)
        {
            services.AddScoped<IOrchestratorService, OrchestratorService>();
            services.AddScoped<ICredentialService, CredentialService>();
            services.AddScoped<IAccountService, AccountService>();

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
