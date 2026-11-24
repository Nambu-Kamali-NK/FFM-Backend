using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.FieldOperations.Infrastructure.Repositories
{
    public class ClientSiteRepository : IClientSiteRepository
    {
        private readonly FieldOperationsDbContext _dbContext;

        public ClientSiteRepository(FieldOperationsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ClientSite?> GetByIdAsync(int id)
        {
            if (id <= 0) return null;
            return await _dbContext.ClientSites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ClientSite?> GetByCodeAsync(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) return null;
            var normalized = siteCode.Trim().ToUpperInvariant();
            return await _dbContext.ClientSites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteCode.ToUpper() == normalized);
        }

        public async Task<bool> ExistsByCodeAsync(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) return false;
            var normalized = siteCode.Trim().ToUpperInvariant();
            return await _dbContext.ClientSites.AnyAsync(s => s.SiteCode.ToUpper() == normalized);
        }

        public async Task<List<ClientSite>> GetAllAsync(bool activeOnly = true)
        {
            var query = _dbContext.ClientSites.AsNoTracking();
            if (activeOnly)
            {
                query = query.Where(s => s.IsActive);
            }
            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task AddAsync(ClientSite clientSite)
        {
            await _dbContext.ClientSites.AddAsync(clientSite);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
