using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateLimiter.Configuration;
using RateLimiter.Interfaces;
using RateLimiter.Services;
using StackExchange.Redis;

namespace RateLimiter.Extensions
{
    public static class ServiceCollectionExtensions
    {
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
