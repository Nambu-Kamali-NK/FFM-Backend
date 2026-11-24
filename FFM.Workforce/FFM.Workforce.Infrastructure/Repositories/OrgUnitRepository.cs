using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FFM.Workforce.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IOrgUnitRepository.
/// </summary>
public class OrgUnitRepository : IOrgUnitRepository
{
    private readonly WorkforceDbContext _db;

    public OrgUnitRepository(WorkforceDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<OrgUnit?> GetDefaultActiveAsync()
    {
        return await _db.OrgUnits
            .FirstOrDefaultAsync(o => o.IsDefault && o.Status == "ACTIVE");
    }
}
