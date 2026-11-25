using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.GPS.Tracking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class TrackingController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrackingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Ingests a live GPS breadcrumb ping from a field employee's mobile device.
    /// </summary>
    [HttpPost("breadcrumbs")]
    [ProducesResponseType(typeof(ApiResponse<BreadcrumbResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordBreadcrumb([FromBody] CreateBreadcrumbRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "BAD_REQUEST",
                "Request body cannot be empty.",
                "Validation error"
            ));
        }

        if (request.Latitude < -90m || request.Latitude > 90m || request.Longitude < -180m || request.Longitude > 180m)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_COORDINATES",
                "Latitude must be between -90 and +90, Longitude between -180 and +180.",
                "Invalid GPS coordinates"
            ));
        }

        // IDOR / Security Rule: If caller is EMPLOYEE, enforce they can only record breadcrumbs for themselves
        var userRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var callerUserIdStr = User.FindFirst("userId")?.Value ?? User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (string.Equals(userRole, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(callerUserIdStr, out var callerUserId))
            {
                if (request.UserId > 0 && request.UserId != callerUserId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                        "FORBIDDEN_EMPLOYEE_ACCESS",
                        "Employees are only permitted to record breadcrumbs for their own account.",
                        "Access denied"
                    ));
                }
                request.UserId = callerUserId;
            }
        }

        if (request.UserId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_USER_ID",
                "User ID must be greater than zero.",
                "Invalid user ID"
            ));
        }

        // Map DTO to MediatR command
        var command = new CreateBreadcrumbCommand
        {
            UserId = request.UserId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            SpeedKmh = request.SpeedKmh,
            HeadingDegrees = request.HeadingDegrees,
            AltitudeMeters = request.AltitudeMeters,
            BatteryPercentage = request.BatteryPercentage,
            ActivityType = request.ActivityType,
            RecordedAt = request.RecordedAt
        };

        var result = await _mediator.Send(command);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BreadcrumbResponseDto>.Ok(result, "Breadcrumb recorded successfully."));
    }

    /// <summary>
    /// Retrieves the travel route history, distance analytics, stops, and polyline for a user on a specific date.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<TravelRouteHistoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTravelRouteHistory(
        [FromQuery] int userId,
        [FromQuery] string date)
    {
        // 1. Basic validation
        if (userId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_USER_ID",
                "User ID must be greater than zero.",
                "Invalid user ID"
            ));
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_DATE_FORMAT",
                "The date parameter is required in 'YYYY-MM-DD' format.",
                "Missing date parameter"
            ));
        }

        // 2. IDOR / Scope Security Rule (BR-HIS-01):
        // If caller is EMPLOYEE, they can only view their own history.
        var userRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var callerUserIdStr = User.FindFirst("userId")?.Value ?? User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (string.Equals(userRole, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
        {
            if (callerUserIdStr != userId.ToString())
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                    "FORBIDDEN_EMPLOYEE_ACCESS",
                    "Employees are only permitted to view their own travel route history.",
                    "Access denied"
                ));
            }
        }

        // 3. Dispatch Query
        try
        {
            var query = new GetTravelRouteHistoryQuery(userId, date, includePolyline: true, minDwellMinutes: 5);
            var result = await _mediator.Send(query);
            return Ok(ApiResponse<TravelRouteHistoryResponseDto>.Ok(result, "Travel route history retrieved successfully."));
        }
        catch (FormatException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_DATE_FORMAT",
                ex.Message,
                "Invalid date format"
            ));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "INVALID_ARGUMENT",
                ex.Message,
                "Bad request"
            ));
        }
    }
}
