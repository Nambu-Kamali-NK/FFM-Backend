using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Infrastructure.Repositories;

public class RosterRepository : IRosterRepository
{
    private readonly WorkforceDbContext _context;

    public RosterRepository(WorkforceDbContext context)
    {
        _context = context;
    }

    public async Task<RosterAssignment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.RosterAssignments
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RosterAssignment?> GetByEmployeeAndDateAsync(int employeeId, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.RosterAssignments
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId 
                                   && r.RosterDate.Date == targetDate 
                                   && r.Status != RosterAssignmentStatus.CANCELLED, cancellationToken);
    }

    public async Task<bool> HasOverlappingRosterAsync(int employeeId, DateTime date, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var query = _context.RosterAssignments
            .Where(r => r.EmployeeId == employeeId 
                     && r.RosterDate.Date == targetDate 
                     && r.Status != RosterAssignmentStatus.CANCELLED);

        if (excludeId.HasValue)
        {
            query = query.Where(r => r.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<List<RosterAssignment>> GetRostersAsync(int? employeeId, DateTime fromDate, DateTime toDate, string? zone = null, CancellationToken cancellationToken = default)
    {
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = _context.RosterAssignments
            .Include(r => r.Employee)
            .Include(r => r.Shift)
            .Where(r => r.RosterDate >= from 
                     && r.RosterDate <= to
                     && r.Status != RosterAssignmentStatus.CANCELLED);

        if (employeeId.HasValue)
        {
            query = query.Where(r => r.EmployeeId == employeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(zone))
        {
            var trimmedZone = zone.Trim().ToLower();
            query = query.Where(r => r.Zone != null && r.Zone.ToLower() == trimmedZone);
        }

        return await query.OrderBy(r => r.RosterDate).ThenBy(r => r.EmployeeId).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RosterAssignment rosterAssignment, CancellationToken cancellationToken = default)
    {
        await _context.RosterAssignments.AddAsync(rosterAssignment, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
