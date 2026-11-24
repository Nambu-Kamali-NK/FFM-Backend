using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.FieldOperations.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class VisitsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;
        private readonly IS3StorageService? _s3StorageService;

        public VisitsController(
            IMediator mediator,
            ICurrentUserService currentUserService,
            IS3StorageService? s3StorageService = null)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
            _s3StorageService = s3StorageService;
        }

        /// <summary>
        /// Executes on-site visit check-in by validating GPS coordinates against the client site geofence.
        /// </summary>
        /// <param name="request">Check-in telemetry payload adhering to zero path parameter standard.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 201 Created on new check-in or HTTP 200 OK on idempotent retry.</returns>
        [HttpPost("site-check-in")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<VisitCheckInResultDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<VisitCheckInResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CheckIn(
            [FromBody] VisitCheckInRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CheckInVisitCommand
            {
                TaskId = request.TaskId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                AccuracyMeters = request.AccuracyMeters,
                IsMockLocation = request.IsMockLocation,
                ActorUserId = _currentUserService.GetUserId(),
                ActorRoles = System.Linq.Enumerable.ToList(_currentUserService.GetRoles())
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                if (result.IsIdempotentReplay)
                {
                    return Ok(ApiResponse<VisitCheckInResultDto>.Ok(result, "Visit is already active in CHECKED_IN status."));
                }
                return StatusCode(StatusCodes.Status201Created, ApiResponse<VisitCheckInResultDto>.Ok(result, "Visit checked in successfully. Technician verified on-site."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("TASK_ACCESS_DENIED", ex.Message, "Authorization error"));
            }
            catch (ArgumentException ex)
            {
                var errorCode = ex.Message.Contains("Latitude", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Longitude", StringComparison.OrdinalIgnoreCase)
                    ? "INVALID_COORDINATES"
                    : "VALIDATION_ERROR";
                return BadRequest(ApiResponse<object>.Fail(errorCode, ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("active checked-in visit", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status409Conflict, ApiResponse<object>.Fail("ACTIVE_VISIT_EXISTS", ex.Message, "Concurrency conflict"));
                }
                if (ex.Message.Contains("meters away from the client site", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("OUTSIDE_GEOFENCE_BOUNDARY", ex.Message, "Geofence violation"));
                }
                if (ex.Message.Contains("Mock GPS", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("MOCK_LOCATION_DETECTED", ex.Message, "Telemetry violation"));
                }
                if (ex.Message.Contains("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("CLIENT_SITE_INACTIVE", ex.Message, "Client site state error"));
                }
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "State transition error"));
            }
        }

        /// <summary>
        /// Completes an on-site visit and closes the associated task order with sign-off and duration calculation.
        /// </summary>
        /// <param name="request">Check-out payload adhering to zero path parameter standard.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the completed visit and task summary.</returns>
        [HttpPost("site-check-out")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<VisitCheckOutResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckOut(
            [FromBody] VisitCheckOutRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CheckOutVisitCommand
            {
                VisitId = request.VisitId,
                TaskId = request.TaskId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CompletionNotes = request.CompletionNotes,
                CustomerName = request.CustomerName,
                CustomerSignatureUrl = request.CustomerSignatureUrl,
                CustomerRating = request.CustomerRating,
                ActorUserId = _currentUserService.GetUserId(),
                ActorRoles = _currentUserService.GetRoles()
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<VisitCheckOutResultDto>.Ok(result, "Visit checked out successfully. Task marked COMPLETED."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("VISIT_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("TASK_ACCESS_DENIED", ex.Message, "Authorization error"));
            }
            catch (ArgumentException ex)
            {
                var errorCode = ex.ParamName switch
                {
                    nameof(request.Latitude) => "INVALID_COORDINATES",
                    nameof(request.Longitude) => "INVALID_COORDINATES",
                    nameof(request.CompletionNotes) => "COMPLETION_NOTES_REQUIRED",
                    nameof(request.CustomerRating) => "VALIDATION_ERROR",
                    nameof(request.CustomerName) => "VALIDATION_ERROR",
                    nameof(request.CustomerSignatureUrl) => "VALIDATION_ERROR",
                    _ => "VALIDATION_ERROR"
                };
                return BadRequest(ApiResponse<object>.Fail(errorCode, ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("already been checked out", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("VISIT_ALREADY_CLOSED", ex.Message, "State transition error"));
                }
                if (ex.Message.Contains("evidence", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("MANDATORY_EVIDENCE_REQUIRED", ex.Message, "Evidence validation error"));
                }
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "State transition error"));
            }
        }

        /// <summary>
        /// Records an on-site visit evidence artifact (photo, customer signature, document, meter reading, or invoice).
        /// </summary>
        /// <param name="request">Evidence capture payload adhering to zero path parameter standard.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 201 Created with the recorded evidence record.</returns>
        [HttpPost("evidence")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<VisitEvidenceDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddEvidence(
            [FromBody] AddVisitEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new AddVisitEvidenceCommand
            {
                VisitId = request.VisitId,
                TaskId = request.TaskId,
                EvidenceTypeId = request.EvidenceTypeId,
                EvidenceType = string.Empty,
                FileName = request.FileName,
                FolderPath = request.FolderPath,
                FileUrl = System.Linq.Enumerable.FirstOrDefault(request.FileUrls ?? new List<string>()) ?? string.Empty,
                FileUrls = request.FileUrls ?? new List<string>(),
                Files = request.Files,
                ThumbnailUrl = request.ThumbnailUrl,
                Remarks = request.Remarks,
                CapturedLatitude = request.CapturedLatitude,
                CapturedLongitude = request.CapturedLongitude,
                Readings = request.Readings,
                ActorUserId = _currentUserService.GetUserId(),
                ActorRoles = System.Linq.Enumerable.ToList(_currentUserService.GetRoles())
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, ApiResponse<VisitEvidenceDto>.Ok(result, "Visit evidence recorded successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("VISIT_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("TASK_ACCESS_DENIED", ex.Message, "Authorization error"));
            }
            catch (ArgumentException ex)
            {
                var errorCode = !string.IsNullOrWhiteSpace(ex.ParamName) && ex.ParamName.StartsWith("readings", StringComparison.OrdinalIgnoreCase)
                    ? "FIELD_VALIDATION_ERROR"
                    : "VALIDATION_ERROR";
                return BadRequest(ApiResponse<object>.Fail(errorCode, ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_VISIT_STATE", ex.Message, "Validation error"));
            }
        }

        /// <summary>
        /// Retrieves the SDUI evidence requirements checklist for a task or task category.
        /// Instructs the mobile client which camera captures, readings, and signatures must be collected.
        /// </summary>
        /// <param name="taskType">Optional task category code (e.g. AC_MAINTENANCE, SOLAR_PANEL, ELEVATOR, GENERAL).</param>
        /// <param name="taskId">Optional task ID to resolve task category automatically.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the dynamic evidence schema.</returns>
        [HttpGet("evidence-schema")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<TaskEvidenceSchemaDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvidenceSchema(
            [FromQuery] string? taskType,
            [FromQuery] int? taskId,
            CancellationToken cancellationToken)
        {
            var query = new GetVisitEvidenceSchemaQuery(taskType, taskId);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<TaskEvidenceSchemaDto>.Ok(result, "Visit evidence schema retrieved successfully."));
        }

        /// <summary>
        /// Retrieves visit evidences filtered by visitId or taskId with optional evidenceType filter.
        /// Technicians (role EMPLOYEE) are strictly restricted to their own assigned visits.
        /// </summary>
        /// <param name="visitId">Optional ID of the visit.</param>
        /// <param name="taskId">Optional ID of the task.</param>
        /// <param name="evidenceType">Optional filter for evidence classification.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the list of visit evidences.</returns>
        [HttpGet("evidence")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VisitEvidenceDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvidences(
            [FromQuery] int? visitId,
            [FromQuery] int? taskId,
            [FromQuery] string? evidenceType,
            CancellationToken cancellationToken)
        {
            var query = new GetVisitEvidencesQuery
            {
                VisitId = visitId,
                TaskId = taskId,
                EvidenceType = evidenceType,
                CallerUserId = _currentUserService.GetUserId(),
                CallerRoles = System.Linq.Enumerable.ToList(_currentUserService.GetRoles())
            };

            try
            {
                var result = await _mediator.Send(query, cancellationToken);
                return Ok(ApiResponse<IReadOnlyList<VisitEvidenceDto>>.Ok(result, "Visit evidences retrieved successfully."));
            }
            catch (ArgumentException ex)
            {
                var errorCode = !string.IsNullOrWhiteSpace(ex.ParamName) && ex.ParamName.StartsWith("readings", StringComparison.OrdinalIgnoreCase)
                    ? "FIELD_VALIDATION_ERROR"
                    : "VALIDATION_ERROR";
                return BadRequest(ApiResponse<object>.Fail(errorCode, ex.Message, "Validation error"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("FORBIDDEN", ex.Message, "Authorization error"));
            }
        }
    }
}
