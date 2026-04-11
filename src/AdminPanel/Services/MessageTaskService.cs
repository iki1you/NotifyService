using Data;
using Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPanel.Services
{
    public class MessageTaskService : IDataService<MessageTask>
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public MessageTaskService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<MessageTask>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MessageTasks
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<MessageTask?> GetByIdAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MessageTasks.FindAsync(id);
        }

        public async Task<MessageTask> CreateAsync(MessageTask entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.CreatedAt = DateTime.UtcNow;
            context.MessageTasks.Add(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<MessageTask> UpdateAsync(MessageTask entity)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            entity.UpdatedAt = DateTime.UtcNow;
            context.MessageTasks.Update(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAsync(long id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.MessageTasks.FindAsync(id);
            if (entity != null)
            {
                context.MessageTasks.Remove(entity);
                await context.SaveChangesAsync();
            }
        }
    }
}
