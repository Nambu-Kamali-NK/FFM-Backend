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
    public class AttendanceController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public AttendanceController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Records workday check-in for the currently authenticated field employee.
        /// </summary>
        [HttpPost("check-in")]
        [ProducesResponseType(typeof(ApiResponse<AttendanceCheckInResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CheckIn(
            [FromBody] AttendanceCheckInRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CheckInCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                CheckInNotes = request.CheckInNotes,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            var result = await _mediator.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<AttendanceCheckInResponseDto>.Ok(result, "Check-in recorded successfully"));
        }

        /// <summary>
        /// Records workday check-out for the currently authenticated field employee and calculates working hours.
        /// </summary>
        [HttpPost("check-out")]
        [ProducesResponseType(typeof(ApiResponse<AttendanceCheckOutResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckOut(
            [FromBody] AttendanceCheckOutRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CheckOutCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                CheckOutNotes = request.CheckOutNotes,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<AttendanceCheckOutResponseDto>.Ok(result, "Check-out recorded successfully"));
        }

        /// <summary>
        /// Starts a break period (LUNCH, REST, PERSONAL) for the currently authenticated field employee.
        /// </summary>
        [HttpPost("break/start")]
        [ProducesResponseType(typeof(ApiResponse<StartBreakResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> StartBreak(
            [FromBody] StartBreakRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new StartBreakCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                BreakType = request.BreakType
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<StartBreakResponseDto>.Ok(result, "Break started successfully"));
        }

        /// <summary>
        /// Ends an active break period for the currently authenticated field employee and calculates break duration.
        /// </summary>
        [HttpPost("break/end")]
        [ProducesResponseType(typeof(ApiResponse<EndBreakResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EndBreak(
            [FromBody] EndBreakRequest? request,
            CancellationToken cancellationToken)
        {
            var command = new EndBreakCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                Notes = request?.Notes
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<EndBreakResponseDto>.Ok(result, "Break ended successfully"));
        }

        /// <summary>
        /// Retrieves paginated attendance history for the currently authenticated field employee.
        /// </summary>
        [HttpGet("my-history")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<AttendanceHistoryDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyHistory(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetMyAttendanceHistoryQuery
            {
                ActorUserId = _currentUserService.GetUserId(),
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<AttendanceHistoryDto>>.Ok(result, "Attendance history retrieved successfully"));
        }

        /// <summary>
        /// Manually corrects or regularizes check-in/check-out timestamps for an attendance record (Manager/Admin only).
        /// </summary>
        [HttpPut("update-attendance")]
        [Authorize(Roles = "MANAGER,ADMIN,SYSTEM_ADMIN")]
        [ProducesResponseType(typeof(ApiResponse<CorrectAttendanceResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CorrectAttendance(
            [FromBody] CorrectAttendanceRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CorrectAttendanceCommand(
                request.AttendanceId,
                request.CheckInTime,
                request.CheckOutTime,
                request.Reason,
                _currentUserService.GetUserId()
            );

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<CorrectAttendanceResponseDto>.Ok(result, "Attendance regularized successfully"));
        }

        /// <summary>
        /// Retrieves paginated team attendance monitoring records for managers and administrators.
        /// </summary>
        [HttpGet("team")]
        [Authorize(Roles = "MANAGER,ADMIN,SYSTEM_ADMIN")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TeamAttendanceDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetTeamAttendance(
            [FromQuery] Guid? orgUnitId,
            [FromQuery] DateTime? date,
            [FromQuery] AttendanceStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetTeamAttendanceQuery
            {
                ActorUserId = _currentUserService.GetUserId(),
                OrgUnitId = orgUnitId,
                Date = date,
                Status = status,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<TeamAttendanceDto>>.Ok(result, "Team attendance retrieved successfully"));
        }
    }
}
