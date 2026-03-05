using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Queue.Configuration;
using Queue.Interfaces;
using Queue.Services;

namespace Queue.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbitMqServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMQ"));

            services.AddSingleton<RabbitMqConnectionFactory>();
            services.AddSingleton<IRabbitMqConnectionFactory>(sp => sp.GetRequiredService<RabbitMqConnectionFactory>());
            services.AddHostedService(sp => sp.GetRequiredService<RabbitMqConnectionFactory>());

            services.AddScoped<IQueuePublisher, QueuePublisher>();
            services.AddTransient<IQueueConsumer, QueueConsumer>();

            return services;
        }
    }
}
