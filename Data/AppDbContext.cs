using Data.Entities;
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
        public DbSet<Credential> Credentials { get; set; }
        public DbSet<ApiToken> ApiTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MessageTask>()
                .Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<MessageTask>()
                .Property(e => e.Channel)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<Credential>(entity =>
            {
                entity.Property(e => e.Channel)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(e => e.AdapterType)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<ApiToken>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
