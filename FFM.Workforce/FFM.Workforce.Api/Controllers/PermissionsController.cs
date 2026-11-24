using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Workforce.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public PermissionsController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Submits a short-duration (0.5 to 4.0 hours) permission request for the authenticated employee.
        /// </summary>
        [HttpPost("requests")]
        [ProducesResponseType(typeof(ApiResponse<PermissionRequestDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SubmitPermissionRequest(
            [FromBody] SubmitPermissionRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                PermissionDate = request.PermissionDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Reason = request.Reason
            };

            var result = await _mediator.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<PermissionRequestDto>.Ok(result, "Permission request submitted successfully and is pending approval."));
        }

        /// <summary>
        /// Retrieves paginated permission requests with optional filters for status, employee, date, and year.
        /// </summary>
        [HttpGet("requests")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<PermissionRequestDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPermissionRequests(
            [FromQuery] int? employeeId,
            [FromQuery] LeaveStatus? status,
            [FromQuery] DateTime? date,
            [FromQuery] int? year,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetPermissionRequestsQuery
            {
                ActorUserId = _currentUserService.GetUserId(),
                EmployeeId = employeeId,
                Status = status,
                Date = date,
                Year = year,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<PermissionRequestDto>>.Ok(result, "Permission requests retrieved successfully"));
        }

        /// <summary>
        /// Records an approval or rejection decision on a pending permission request (Managers/Admins only).
        /// </summary>
        [HttpPut("requests/decision")]
        [ProducesResponseType(typeof(ApiResponse<PermissionRequestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DecidePermissionRequest(
            [FromBody] PermissionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            int targetRequestId = request.RequestId > 0 ? request.RequestId : request.Id;
            if (targetRequestId <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "A valid RequestId or Id is required in the request body.", "Validation error"));
            }

            var command = new DecidePermissionRequestCommand
            {
                RequestId = targetRequestId,
                ActorUserId = _currentUserService.GetUserId(),
                Decision = request.Decision,
                Comments = request.Comments
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<PermissionRequestDto>.Ok(result, $"Permission request {result.Status.ToLower()} successfully."));
        }

        /// <summary>
        /// Cancels a pending permission request, or a future approved permission request.
        /// </summary>
        [HttpPost("requests/cancel")]
        [ProducesResponseType(typeof(ApiResponse<PermissionRequestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelPermissionRequest(
            [FromBody] CancelPermissionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            int targetRequestId = request.RequestId > 0 ? request.RequestId : request.Id;
            if (targetRequestId <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "A valid RequestId or Id is required in the request body.", "Validation error"));
            }

            var command = new CancelPermissionRequestCommand
            {
                RequestId = targetRequestId,
                ActorUserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<PermissionRequestDto>.Ok(result, "Permission request cancelled successfully."));
        }
    }
}
