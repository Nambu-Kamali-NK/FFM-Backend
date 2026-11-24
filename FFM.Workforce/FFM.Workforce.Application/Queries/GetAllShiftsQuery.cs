using System.Collections.Generic;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries;

/// <summary>
/// Query to retrieve all municipal shifts with optional filtering.
/// Adheres strictly to Zero Path Parameters standard.
/// </summary>
public class GetAllShiftsQuery : IRequest<List<ShiftDto>>
{
    public bool? IsActive { get; set; } = true;
    public bool IncludeRestDays { get; set; } = true;
}
