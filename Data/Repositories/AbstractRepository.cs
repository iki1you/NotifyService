using Abstractions.Models;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Data.Repositories
{
    public abstract class AbstractRepository<T> : IRepository<T> where T : class, IEntity
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        protected AbstractRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        protected virtual IQueryable<T> DefaultIncludes(IQueryable<T> query)
        {
            return query;
        }

        public virtual IQueryable<T> AsQueryable()
        {
            return DefaultIncludes(_dbSet.AsQueryable());
        }

        public virtual async Task<T?> GetByIdAsync(long id)
        {
            var query = DefaultIncludes(_dbSet.AsQueryable());
            return await query.FirstOrDefaultAsync(e => e.Id == id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = DefaultIncludes(_dbSet.AsQueryable());
            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var query = DefaultIncludes(_dbSet.AsQueryable());
            return await query.Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            var query = DefaultIncludes(_dbSet.AsQueryable());
            return await query.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
