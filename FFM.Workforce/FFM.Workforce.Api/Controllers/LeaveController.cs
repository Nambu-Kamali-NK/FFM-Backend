using System;
using System.Collections.Generic;
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
    public class LeaveController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public LeaveController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Retrieves all configured leave types and their yearly entitlements.
        /// </summary>
        [HttpGet("types")]
        [ProducesResponseType(typeof(ApiResponse<List<LeaveTypeDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeaveTypes(CancellationToken cancellationToken)
        {
            var query = new GetLeaveTypesQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<List<LeaveTypeDto>>.Ok(result, "Leave types retrieved successfully"));
        }

        /// <summary>
        /// Retrieves leave balances (total, used, available) for the current user or a specified subordinate.
        /// </summary>
        [HttpGet("balances")]
        [ProducesResponseType(typeof(ApiResponse<List<LeaveBalanceDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLeaveBalances(
            [FromQuery] int? employeeId,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            var query = new GetLeaveBalancesQuery
            {
                ActorUserId = _currentUserService.GetUserId(),
                TargetEmployeeId = employeeId,
                Year = year
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<List<LeaveBalanceDto>>.Ok(result, "Leave balances retrieved successfully"));
        }

        /// <summary>
        /// Submits a new leave request for the authenticated employee.
        /// </summary>
        [HttpPost("requests")]
        [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SubmitLeaveRequest(
            [FromBody] SubmitLeaveRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new SubmitLeaveRequestCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                LeaveTypeId = request.LeaveTypeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsHalfDay = request.IsHalfDay,
                Reason = request.Reason
            };

            var result = await _mediator.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<LeaveRequestDto>.Ok(result, "Leave request submitted successfully and is pending approval."));
        }

        /// <summary>
        /// Retrieves paginated leave requests with optional filters for status, employee, and year.
        /// </summary>
        [HttpGet("requests")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<LeaveRequestDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetLeaveRequests(
            [FromQuery] int? employeeId,
            [FromQuery] LeaveStatus? status,
            [FromQuery] int? year,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetLeaveRequestsQuery
            {
                ActorUserId = _currentUserService.GetUserId(),
                EmployeeId = employeeId,
                Status = status,
                Year = year,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<LeaveRequestDto>>.Ok(result, "Leave requests retrieved successfully"));
        }

        /// <summary>
        /// Records an approval or rejection decision on a pending leave request (Managers/Admins only).
        /// </summary>
        [HttpPut("requests/decision")]
        [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DecideLeaveRequest(
            [FromBody] LeaveDecisionRequest request,
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

            var command = new DecideLeaveRequestCommand
            {
                RequestId = targetRequestId,
                ActorUserId = _currentUserService.GetUserId(),
                Decision = request.Decision,
                Comments = request.Comments
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<LeaveRequestDto>.Ok(result, $"Leave request {result.Status.ToLower()} successfully."));
        }

        /// <summary>
        /// Cancels a pending leave request, or a future approved leave request with automatic balance refund.
        /// </summary>
        [HttpPost("requests/cancel")]
        [ProducesResponseType(typeof(ApiResponse<LeaveRequestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelLeaveRequest(
            [FromBody] CancelLeaveRequest request,
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

            var command = new CancelLeaveRequestCommand
            {
                RequestId = targetRequestId,
                ActorUserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<LeaveRequestDto>.Ok(result, "Leave request cancelled successfully."));
        }
    }
}
