using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Queue.Configuration;
using Queue.Interfaces;
using Queue.Services;
using StackExchange.Redis;

namespace Queue.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbitMqServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMQ"));
            services.Configure<RetryPolicyOptions>(configuration.GetSection("RetryPolicy"));

            services.AddSingleton<RabbitMqConnectionFactory>();
            services.AddSingleton<IRabbitMqConnectionFactory>(sp => sp.GetRequiredService<RabbitMqConnectionFactory>());
            services.AddHostedService(sp => sp.GetRequiredService<RabbitMqConnectionFactory>());

            services.AddScoped<IQueuePublisher, QueuePublisher>();
            services.AddTransient<IQueueConsumer, QueueConsumer>();

            return services;
        }

        public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<RateLimitOptions>>().CurrentValue;
                var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";

                var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectRetry = 3;
                redisOptions.ConnectTimeout = Math.Max(100, options.RedisRequestTimeoutMs);
                redisOptions.SyncTimeout = Math.Max(100, options.RedisRequestTimeoutMs);

                return ConnectionMultiplexer.Connect(redisOptions);
            });

            services.AddSingleton<IRateLimiter, RedisRateLimiter>();

            return services;
        }
    }
}
