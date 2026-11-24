using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands;

public class AssignRosterCommand : IRequest<RosterAssignmentDto>
{
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateTime RosterDate { get; set; }
    public string? Zone { get; set; }
    public string? Notes { get; set; }
    public int ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
}
