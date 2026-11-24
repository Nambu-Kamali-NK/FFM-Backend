using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Infrastructure.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly WorkforceDbContext _context;

    public ShiftRepository(WorkforceDbContext context)
    {
        _context = context;
    }

    public async Task<List<Shift>> GetAllShiftsAsync(
        bool? isActive = null,
        bool includeRestDays = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Shifts.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        if (!includeRestDays)
        {
            query = query.Where(s => !s.IsRestDay);
        }

        return await query.OrderBy(s => s.Id).ToListAsync(cancellationToken);
    }

    public async Task<Shift?> GetShiftByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Shift?> GetShiftByCodeAsync(string shiftCode, CancellationToken cancellationToken = default)
    {
        return await _context.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ShiftCode == shiftCode, cancellationToken);
    }
}
