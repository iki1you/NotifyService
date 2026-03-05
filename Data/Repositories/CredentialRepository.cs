using Abstractions.Models;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories
{
    public class CredentialRepository : AbstractRepository<Credential>, ICredentialRepository
    {
        public CredentialRepository(AppDbContext context) : base(context)
        {
        }

        protected override IQueryable<Credential> DefaultIncludes(IQueryable<Credential> query)
        {
            return query;
        }

        public async Task<List<CredentialShortInfo>?> GetCredentialsByProjectAndChannelAsync(long projectId, string channel)
        {
            return await _dbSet
                .Where(c => c.ProjectId == projectId && c.Channel == channel && c.IsActive)
                .Select(c => new CredentialShortInfo
                {
                    CredentialId = c.Id,
                    AdapterType = c.AdapterType
                })
                .ToListAsync();
        }

        public async Task<CredentialShortInfo> AddCredentialAsync(long projectId, string channel, long adapterType, string config)
        {
            var credential = new Credential
            {
                ProjectId = projectId,
                Channel = channel,
                AdapterType = adapterType,
                Config = config,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _dbSet.AddAsync(credential);
            await _context.SaveChangesAsync();

            return new CredentialShortInfo
            {
                CredentialId = credential.Id,
                AdapterType = credential.AdapterType
            };
        }
    }
}
