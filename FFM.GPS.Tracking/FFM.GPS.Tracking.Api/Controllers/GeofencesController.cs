using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.GPS.Tracking.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class GeofencesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GeofencesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Validates if an employee's GPS coordinates fall within an assigned or active geofence boundary.
        /// </summary>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiResponse<ValidateGeofenceResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ValidateGeofence([FromBody] ValidateGeofenceRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            if (request.Latitude < -90.0 || request.Latitude > 90.0 || request.Longitude < -180.0 || request.Longitude > 180.0)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_COORDINATES", "Latitude must be between -90 and +90, Longitude between -180 and +180.", "Invalid GPS coordinates"));
            }

            var command = new ValidateGeofenceCommand
            {
                EmployeeId = request.EmployeeId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                GpsAccuracyMeters = request.GpsAccuracyMeters
            };

            var result = await _mediator.Send(command);
            return Ok(ApiResponse<ValidateGeofenceResponseDto>.Ok(result, "Geofence validation processed successfully"));
        }

        /// <summary>
        /// Creates a new Circular or Polygon geofence (Admin/Manager only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        [ProducesResponseType(typeof(ApiResponse<GeofenceResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateGeofence([FromBody] CreateGeofenceRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            try
            {
                var command = new CreateGeofenceCommand
                {
                    LocationRef = request.LocationRef,
                    Name = request.Name,
                    Type = request.Type,
                    RadiusMeters = request.RadiusMeters,
                    Coordinates = request.Coordinates
                };

                var result = await _mediator.Send(command);
                return StatusCode(StatusCodes.Status201Created, ApiResponse<GeofenceResponseDto>.Ok(result, "Geofence created successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Invalid geofence parameters"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail("CONFLICT", ex.Message, "Geofence conflict"));
            }
        }

        /// <summary>
        /// Assigns an active geofence site to an employee (Admin/Manager only).
        /// </summary>
        [HttpPost("assign")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AssignGeofence([FromBody] AssignGeofenceRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            try
            {
                var command = new AssignGeofenceCommand
                {
                    EmployeeId = request.EmployeeId,
                    GeofenceId = request.GeofenceId
                };

                var result = await _mediator.Send(command);
                return Ok(ApiResponse<object>.Ok(new { request.EmployeeId, request.GeofenceId }, "Geofence assigned to employee successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ex.Message, "Resource not found"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail("CONFLICT", ex.Message, "Already assigned"));
            }
        }

        /// <summary>
        /// Retrieves all active geofence boundaries.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<GeofenceResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetGeofences()
        {
            var result = await _mediator.Send(new GetGeofencesQuery());
            return Ok(ApiResponse<List<GeofenceResponseDto>>.Ok(result, "Active geofences retrieved successfully"));
        }

        /// <summary>
        /// Retrieves history of geofence breach events with filtering and pagination.
        /// </summary>
        [HttpGet("breaches")]
        [ProducesResponseType(typeof(ApiResponse<PagedBreachesResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetBreaches(
            [FromQuery] int? userId,
            [FromQuery] int? geofenceId,
            [FromQuery] string? breachType,
            [FromQuery] bool? isAcknowledged,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userRole = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var callerUserIdStr = User.FindFirst("userId")?.Value ?? User.FindFirst("user_id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

            // IDOR Protection: Employees can only view their own breaches
            if (string.Equals(userRole, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(callerUserIdStr, out var callerUserId))
                {
                    if (userId.HasValue && userId.Value != callerUserId)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                            "FORBIDDEN_EMPLOYEE_ACCESS",
                            "Employees are only permitted to view their own geofence breaches.",
                            "Access denied"));
                    }
                    userId = callerUserId;
                }
            }

            var query = new GetGeofenceBreachesQuery
            {
                UserId = userId,
                GeofenceId = geofenceId,
                BreachType = breachType,
                IsAcknowledged = isAcknowledged,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(ApiResponse<PagedBreachesResultDto>.Ok(result, "Geofence breaches retrieved successfully"));
        }

        /// <summary>
        /// Acknowledges an active geofence breach alert (Admin/Manager/Supervisor only). Zero path parameters rule enforced.
        /// </summary>
        [HttpPut("breaches/acknowledge")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR")]
        [ProducesResponseType(typeof(ApiResponse<GeofenceBreachResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AcknowledgeBreach([FromBody] AcknowledgeBreachRequest request)
        {
            if (request == null || request.BreachId <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Valid breachId is required in request body.", "Validation error"));
            }

            var callerUsername = User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "SystemSupervisor";

            try
            {
                var command = new AcknowledgeBreachCommand
                {
                    BreachId = request.BreachId,
                    AcknowledgedBy = callerUsername,
                    Notes = request.Notes
                };

                var result = await _mediator.Send(command);
                return Ok(ApiResponse<GeofenceBreachResponseDto>.Ok(result, "Geofence breach alert acknowledged successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ex.Message, "Resource not found"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Invalid parameter"));
            }
        }
    }
}
