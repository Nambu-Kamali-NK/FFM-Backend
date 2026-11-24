using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetMyAttendanceHistoryQueryHandler : IRequestHandler<GetMyAttendanceHistoryQuery, PagedResult<AttendanceHistoryDto>>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;

        public GetMyAttendanceHistoryQueryHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
        }

        public async Task<PagedResult<AttendanceHistoryDto>> Handle(GetMyAttendanceHistoryQuery request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee by ActorUserId (BR-HIS-01 isolation guard)
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee record not found for User ID {request.ActorUserId}.");
            }

            // 2. Query attendance history
            var (records, totalCount) = await _attendanceRepository.GetHistoryByEmployeeIdAsync(
                employee.Id,
                request.StartDate,
                request.EndDate,
                request.Page,
                request.PageSize);

            // 3. Map to DTOs
            var items = records.Select(a => new AttendanceHistoryDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                WorkDate = a.WorkDate.ToString("yyyy-MM-dd"),
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Status = a.Status.ToString(),
                TotalWorkingHours = a.TotalWorkingHours,
                TotalBreakMinutes = a.TotalBreakMinutes,
                CheckInNotes = a.CheckInNotes,
                CheckOutNotes = a.CheckOutNotes,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                Breaks = a.Breaks.Select(b => new BreakDto
                {
                    Id = b.Id,
                    BreakType = b.BreakType,
                    BreakStartTime = b.BreakStartTime,
                    BreakEndTime = b.BreakEndTime,
                    DurationMinutes = b.DurationMinutes
                }).ToList()
            }).ToList();

            return new PagedResult<AttendanceHistoryDto>(items, totalCount, request.Page, request.PageSize);
        }
    }
}
