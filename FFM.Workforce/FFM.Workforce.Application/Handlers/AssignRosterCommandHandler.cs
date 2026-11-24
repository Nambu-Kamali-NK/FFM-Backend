using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers;

public class AssignRosterCommandHandler : IRequestHandler<AssignRosterCommand, RosterAssignmentDto>
{
    private readonly IRosterRepository _rosterRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IShiftRepository _shiftRepository;
    private readonly ILeaveRepository _leaveRepository;

    public AssignRosterCommandHandler(
        IRosterRepository rosterRepository,
        IEmployeeRepository employeeRepository,
        IShiftRepository shiftRepository,
        ILeaveRepository leaveRepository)
    {
        _rosterRepository = rosterRepository;
        _employeeRepository = employeeRepository;
        _shiftRepository = shiftRepository;
        _leaveRepository = leaveRepository;
    }

    public async Task<RosterAssignmentDto> Handle(AssignRosterCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate employee exists
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
        if (employee == null)
        {
            throw new KeyNotFoundException($"Employee with Id {request.EmployeeId} not found.");
        }

        // 2. Validate employee is ACTIVE (BR-2)
        if (employee.Status != EmployeeStatus.ACTIVE)
        {
            throw new InvalidOperationException("Only active employees can be assigned to a shift roster.");
        }

        // 3. Validate shift exists
        var shift = await _shiftRepository.GetShiftByIdAsync(request.ShiftId, cancellationToken);
        if (shift == null)
        {
            throw new KeyNotFoundException($"Shift with Id {request.ShiftId} not found.");
        }

        // 4. Validate shift is active
        if (!shift.IsActive)
        {
            throw new InvalidOperationException("Cannot assign an inactive shift template.");
        }

        // 5. Shift Modification Guard: Cannot assign or modify shifts for today or past dates
        var targetDate = request.RosterDate.Date;
        var today = DateTime.UtcNow.Date;
        if (targetDate <= today)
        {
            throw new InvalidOperationException("Shifts for the present date (today) or past dates cannot be assigned or modified. Modifications are strictly restricted to upcoming days (tomorrow onwards).");
        }

        // 6. Leave Conflict Guard (BR-8 / BR-9: Employee on leave cannot be rostered)
        bool hasLeave = await _leaveRepository.HasOverlappingLeaveAsync(employee.Id, targetDate, targetDate);
        if (hasLeave)
        {
            throw new InvalidOperationException("Cannot assign roster: employee has an approved or pending leave on this date.");
        }

        var actor = string.IsNullOrWhiteSpace(request.ActorRole) 
            ? $"USER_{request.ActorUserId}" 
            : $"USER_{request.ActorUserId} ({request.ActorRole})";

        // 7. Update Existing Future Shift or Create New Assignment
        var existingRoster = await _rosterRepository.GetByEmployeeAndDateAsync(employee.Id, targetDate, cancellationToken);
        if (existingRoster != null)
        {
            // Update existing future assignment
            existingRoster.UpdateShift(shift.Id, request.Zone, request.Notes, actor);
            await _rosterRepository.SaveChangesAsync(cancellationToken);

            return new RosterAssignmentDto
            {
                Id = existingRoster.Id,
                EmployeeId = employee.Id,
                EmployeeCode = employee.EmployeeCode,
                EmployeeName = employee.Name,
                ShiftId = shift.Id,
                ShiftCode = shift.ShiftCode,
                ShiftName = shift.Name,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                ColorCode = shift.ColorCode,
                RosterDate = existingRoster.RosterDate,
                Zone = existingRoster.Zone,
                Notes = existingRoster.Notes,
                Status = existingRoster.Status.ToString(),
                CreatedAt = existingRoster.CreatedAt,
                CreatedBy = existingRoster.CreatedBy
            };
        }

        // 8. Duplicate / Overlapping Roster Guard (BR-5)
        bool hasOverlapping = await _rosterRepository.HasOverlappingRosterAsync(employee.Id, targetDate, null, cancellationToken);
        if (hasOverlapping)
        {
            throw new InvalidOperationException("A roster assignment already exists for this employee on the specified date.");
        }

        // 9. Persist new future assignment
        var roster = new RosterAssignment(
            employee.Id,
            shift.Id,
            targetDate,
            request.Zone,
            request.Notes,
            createdBy: actor,
            status: RosterAssignmentStatus.ASSIGNED);

        await _rosterRepository.AddAsync(roster, cancellationToken);
        await _rosterRepository.SaveChangesAsync(cancellationToken);

        // 8. Return response DTO
        return new RosterAssignmentDto
        {
            Id = roster.Id,
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.Name,
            ShiftId = shift.Id,
            ShiftCode = shift.ShiftCode,
            ShiftName = shift.Name,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            ColorCode = shift.ColorCode,
            RosterDate = roster.RosterDate,
            Zone = roster.Zone,
            Notes = roster.Notes,
            Status = roster.Status.ToString(),
            CreatedAt = roster.CreatedAt,
            CreatedBy = roster.CreatedBy
        };
    }
}
