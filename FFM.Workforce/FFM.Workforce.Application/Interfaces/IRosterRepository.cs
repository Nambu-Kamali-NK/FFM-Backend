using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Application.Interfaces;

public interface IRosterRepository
{
    Task<RosterAssignment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RosterAssignment?> GetByEmployeeAndDateAsync(int employeeId, DateTime date, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRosterAsync(int employeeId, DateTime date, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<List<RosterAssignment>> GetRostersAsync(int? employeeId, DateTime fromDate, DateTime toDate, string? zone = null, CancellationToken cancellationToken = default);
    Task AddAsync(RosterAssignment rosterAssignment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
