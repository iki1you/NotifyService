using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        public async Task<List<CredentialShortInfo>?> GetCredentialsByProjectAndChannelAsync(long projectId, ChannelType channel)
        {
            return await _dbSet
                .Where(c => c.ProjectId == projectId
                    && c.IsActive
                    && c.Channel == channel)
                .Select(c => new CredentialShortInfo
                {
                    CredentialId = c.Id,
                    AdapterType = c.AdapterType
                })
                .ToListAsync();
        }

        public async Task<CredentialShortInfo> AddCredentialAsync(long projectId, ChannelType channel, AdapterType adapterType, JsonDocument config)
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

        public async Task<List<long>> GetActiveCredentialIdsByAdapterTypeAsync(AdapterType adapterType, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(c => c.AdapterType == adapterType && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync(ct);
        }
    }
}
