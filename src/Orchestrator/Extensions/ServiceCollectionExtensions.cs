using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestrator.Interfaces;
using Orchestrator.Services;
using Orchestrator.Workers;

namespace Orchestrator.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOrchestratorServices(this IServiceCollection services)
        {
            services.AddScoped<IOrchestratorService, OrchestratorService>();
            services.AddScoped<ICredentialService, CredentialService>();
            services.AddScoped<IAccountService, AccountService>();

            services.AddHostedService<MessageStatusWorker>();

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
