using Data;
using Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPanel.Services
{
    public class MessageRequestService : IDataService<MessageRequest>
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public MessageRequestService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<MessageRequest>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MessageRequests
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<MessageRequest?> GetByIdAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MessageRequests.FindAsync(id);
        }

        public async Task<MessageRequest> CreateAsync(MessageRequest entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.CreatedAt = DateTime.UtcNow;
            context.MessageRequests.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<MessageRequest> UpdateAsync(MessageRequest entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.UpdatedAt = DateTime.UtcNow;
            context.MessageRequests.Update(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.MessageRequests.FindAsync(id);
            if (entity != null)
            {
                context.MessageRequests.Remove(entity);
                await context.SaveChangesAsync();
            }
        }
    }
}
