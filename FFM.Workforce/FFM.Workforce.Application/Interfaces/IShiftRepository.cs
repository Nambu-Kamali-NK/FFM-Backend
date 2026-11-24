using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Application.Interfaces;

public interface IShiftRepository
{
    Task<List<Shift>> GetAllShiftsAsync(bool? isActive = null, bool includeRestDays = true, CancellationToken cancellationToken = default);
    Task<Shift?> GetShiftByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Shift?> GetShiftByCodeAsync(string shiftCode, CancellationToken cancellationToken = default);
}
