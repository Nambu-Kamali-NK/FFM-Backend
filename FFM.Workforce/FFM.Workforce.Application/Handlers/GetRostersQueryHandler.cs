using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers;

public class GetRostersQueryHandler : IRequestHandler<GetRostersQuery, List<RosterAssignmentDto>>
{
    private readonly IRosterRepository _rosterRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public GetRostersQueryHandler(
        IRosterRepository rosterRepository,
        IEmployeeRepository employeeRepository)
    {
        _rosterRepository = rosterRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<List<RosterAssignmentDto>> Handle(GetRostersQuery request, CancellationToken cancellationToken)
    {
        // 1. Resolve date window (defaults to current week Monday through Sunday)
        DateTime fromDate;
        DateTime toDate;

        if (request.From.HasValue && request.To.HasValue)
        {
            fromDate = DateTime.SpecifyKind(request.From.Value.Date, DateTimeKind.Utc);
            toDate = DateTime.SpecifyKind(request.To.Value.Date, DateTimeKind.Utc);
        }
        else if (request.From.HasValue)
        {
            fromDate = DateTime.SpecifyKind(request.From.Value.Date, DateTimeKind.Utc);
            toDate = DateTime.SpecifyKind(fromDate.AddDays(6), DateTimeKind.Utc);
        }
        else if (request.To.HasValue)
        {
            toDate = DateTime.SpecifyKind(request.To.Value.Date, DateTimeKind.Utc);
            fromDate = DateTime.SpecifyKind(toDate.AddDays(-6), DateTimeKind.Utc);
        }
        else
        {
            var today = DateTime.UtcNow.Date;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            fromDate = DateTime.SpecifyKind(today.AddDays(-diff), DateTimeKind.Utc); // Current week's Monday
            toDate = DateTime.SpecifyKind(fromDate.AddDays(6), DateTimeKind.Utc);     // Current week's Sunday
        }

        // 2. Self-scoping for EMPLOYEE role (BR-RST-04)
        int? targetEmployeeId = request.EmployeeId;
        var roles = request.ActorRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool isEmployeeOnly = roles.Length > 0 && roles.All(r => r.Equals("EMPLOYEE", StringComparison.OrdinalIgnoreCase));

        if (isEmployeeOnly)
        {
            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (callerEmployee == null)
            {
                throw new KeyNotFoundException("No employee profile found for the authenticated user.");
            }
            targetEmployeeId = callerEmployee.Id;
        }

        // 3. Fetch from repository
        var rosters = await _rosterRepository.GetRostersAsync(targetEmployeeId, fromDate, toDate, request.Zone, cancellationToken);

        // 4. Map to DTOs
        return rosters.Select(r => new RosterAssignmentDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeCode = r.Employee?.EmployeeCode ?? string.Empty,
            EmployeeName = r.Employee?.Name ?? string.Empty,
            ShiftId = r.ShiftId,
            ShiftCode = r.Shift?.ShiftCode ?? string.Empty,
            ShiftName = r.Shift?.Name ?? string.Empty,
            StartTime = r.Shift?.StartTime,
            EndTime = r.Shift?.EndTime,
            ColorCode = r.Shift?.ColorCode ?? "#1C585C",
            RosterDate = r.RosterDate,
            Zone = r.Zone,
            Notes = r.Notes,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy
        }).ToList();
    }
}
