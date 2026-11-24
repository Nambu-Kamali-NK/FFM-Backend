using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ShiftsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieves all configured municipal shift blueprints.
    /// Strictly adheres to Zero Path Parameters standard.
    /// </summary>
    /// <param name="query">Optional query parameters: isActive, includeRestDays.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with list of municipal shifts.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ShiftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllShiftsQuery? query,
        CancellationToken cancellationToken)
    {
        query ??= new GetAllShiftsQuery();
        var shifts = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<List<ShiftDto>>.Ok(shifts, "Shifts retrieved successfully."));
    }
}
