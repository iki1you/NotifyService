using Abstractions.Models;
using Abstractions.Models.QueueEntities;
using Microsoft.EntityFrameworkCore;

namespace Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options)
        {
        }

        public DbSet<MessageRequest> MessageRequests { get; set; }
        public DbSet<MessageTask> MessageTasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MessageRequest>()
                .HasIndex(e => e.RequestId)
                .IsUnique();

            modelBuilder.Entity<MessageTask>()
                .HasIndex(e => e.RequestId);
        }
    }
}
