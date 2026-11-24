using System;
using System.Collections.Generic;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries;

public class GetRostersQuery : IRequest<List<RosterAssignmentDto>>
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? EmployeeId { get; set; }
    public string? Zone { get; set; }
    public int ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
}
