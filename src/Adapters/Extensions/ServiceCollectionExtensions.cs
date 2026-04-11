using Adapters.GreenAPI.Services;
using Adapters.GreenAPI.HttpClients;
using Microsoft.Extensions.DependencyInjection;
using Adapters.Interfaces;
using Adapters.Services;

namespace Adapters.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAdapterServices(this IServiceCollection services)
        {
            services.AddScoped<IGreenApiSendService, GreenApiSendService>();
            services.AddScoped<CredentialService>();
            services.AddScoped<GreenApiHttpClient>();

            return services;
        }
    }
}
