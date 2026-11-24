using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class RostersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RostersController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Retrieves shift roster assignments for a specified date range and optional employee/zone filter.
    /// Strictly adheres to Zero Path Parameters standard and RBAC role restrictions.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
    [ProducesResponseType(typeof(ApiResponse<List<RosterAssignmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRosters(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? employeeId,
        [FromQuery] string? zone,
        CancellationToken cancellationToken)
    {
        var query = new GetRostersQuery
        {
            From = from,
            To = to,
            EmployeeId = employeeId,
            Zone = zone,
            ActorUserId = _currentUserService.GetUserId(),
            ActorRole = string.Join(",", _currentUserService.GetRoles())
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<List<RosterAssignmentDto>>.Ok(result, "Roster assignments retrieved successfully."));
    }

    /// <summary>
    /// Assigns a field employee to a specific shift and operational zone for a calendar date.
    /// Strictly adheres to Zero Path Parameters standard and RBAC role restrictions.
    /// </summary>
    /// <param name="request">Roster assignment payload containing employeeId, shiftId, rosterDate, zone, notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with the created roster assignment details.</returns>
    [HttpPost("assign")]
    [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR")]
    [ProducesResponseType(typeof(ApiResponse<RosterAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(
        [FromBody] AssignRosterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignRosterCommand
        {
            EmployeeId = request.EmployeeId,
            ShiftId = request.ShiftId,
            RosterDate = request.RosterDate,
            Zone = request.Zone,
            Notes = request.Notes,
            ActorUserId = _currentUserService.GetUserId(),
            ActorRole = string.Join(",", _currentUserService.GetRoles())
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<RosterAssignmentDto>.Ok(result, "Roster assigned successfully."));
    }
}
