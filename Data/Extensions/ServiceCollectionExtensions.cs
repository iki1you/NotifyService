using Abstractions.Models;
using Abstractions.Models.QueueEntities;
using Data.Interfaces;
using Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Data.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IRepository<MessageRequest>, MessageRequestRepository>();
            services.AddScoped<IRepository<MessageTask>, MessageTaskRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();

            return services;
        }
    }
}
