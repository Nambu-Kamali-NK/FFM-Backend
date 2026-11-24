using FFM.Workforce.Domain.Entities;
using System.Threading.Tasks;

namespace FFM.Workforce.Application.Interfaces;

/// <summary>
/// Repository for OrgUnit read operations used by application handlers.
/// </summary>
public interface IOrgUnitRepository
{
    /// <summary>
    /// Returns the single active default OrgUnit, or null if none exists.
    /// </summary>
    Task<OrgUnit?> GetDefaultActiveAsync();
}
