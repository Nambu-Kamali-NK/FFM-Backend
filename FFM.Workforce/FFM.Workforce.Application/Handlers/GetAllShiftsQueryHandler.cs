using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;

namespace FFM.Workforce.Application.Handlers;

public class GetAllShiftsQueryHandler : IRequestHandler<GetAllShiftsQuery, List<ShiftDto>>
{
    private readonly IShiftRepository _shiftRepository;

    public GetAllShiftsQueryHandler(IShiftRepository shiftRepository)
    {
        _shiftRepository = shiftRepository;
    }

    public async Task<List<ShiftDto>> Handle(GetAllShiftsQuery request, CancellationToken cancellationToken)
    {
        var shifts = await _shiftRepository.GetAllShiftsAsync(
            request.IsActive,
            request.IncludeRestDays,
            cancellationToken);

        return shifts.Select(s => new ShiftDto
        {
            Id = s.Id,
            ShiftCode = s.ShiftCode,
            Name = s.Name,
            Description = s.Description,
            StartTime = s.StartTime.ToString(@"hh\:mm\:ss"),
            EndTime = s.EndTime.ToString(@"hh\:mm\:ss"),
            DurationHours = s.CalculateDurationHours(),
            GracePeriodMinutes = s.GracePeriodMinutes,
            IsNightShift = s.IsNightShift,
            IsRestDay = s.IsRestDay,
            ColorCode = s.ColorCode,
            IsActive = s.IsActive
        }).ToList();
    }
}
