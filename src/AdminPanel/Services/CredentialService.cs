using Data;
using Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPanel.Services
{
    public class CredentialService : IDataService<Credential>
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CredentialService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Credential>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Credentials
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<Credential?> GetByIdAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Credentials.FindAsync(id);
        }

        public async Task<Credential> CreateAsync(Credential entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.CreatedAt = DateTime.UtcNow;
            context.Credentials.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<Credential> UpdateAsync(Credential entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.UpdatedAt = DateTime.UtcNow;
            context.Credentials.Update(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.Credentials.FindAsync(id);
            if (entity != null)
            {
                context.Credentials.Remove(entity);
                await context.SaveChangesAsync();
            }
        }
    }
}
