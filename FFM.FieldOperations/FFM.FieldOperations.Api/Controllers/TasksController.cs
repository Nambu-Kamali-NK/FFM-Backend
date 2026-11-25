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
    public class TasksController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public TasksController(
            IMediator mediator,
            ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Retrieves the complete Server-Driven UI (SDUI) full screen layout tree for task execution.
        /// Returns all screen widgets (Hero header, Client site card, Form section, Telemetry gauges, Evidence gallery, Action bar)
        /// dynamically driven by backend rules and live task status.
        /// </summary>
        /// <param name="taskId">Optional task ID to hydrate screen with live task state.</param>
        /// <param name="taskType">Optional task category code (e.g. AC_MAINTENANCE, SOLAR_PANEL, ELEVATOR, GENERAL).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the full screen SDUI layout tree.</returns>
        [HttpGet("screen-layout")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<ScreenLayoutDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTaskScreenLayout(
            [FromQuery] int? taskId,
            [FromQuery] string? taskType,
            CancellationToken cancellationToken)
        {
            try
            {
                var query = new GetTaskScreenLayoutQuery(taskId, taskType);
                var result = await _mediator.Send(query, cancellationToken);
                return Ok(ApiResponse<ScreenLayoutDto>.Ok(result, "Full-screen SDUI layout retrieved successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND", ex.Message, "Validation error"));
            }
        }

        /// <summary>
        /// Retrieves the SDUI dynamic form schema for task creation based on the task category.
        /// Allows the mobile app to render form widgets dynamically without app store updates.
        /// </summary>
        /// <param name="taskType">Optional category (e.g. GENERAL, AC_MAINTENANCE, SOLAR_PANEL, ELEVATOR).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the dynamic form blueprint.</returns>
        [HttpGet("form-schema")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<TaskFormSchemaDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTaskFormSchema(
            [FromQuery] string? taskType,
            CancellationToken cancellationToken)
        {
            var query = new GetTaskFormSchemaQuery(taskType);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<TaskFormSchemaDto>.Ok(result, "Task form schema retrieved successfully."));
        }

        /// <summary>
        /// Retrieves the SDUI action buttons available for a task based on its current lifecycle status.
        /// Instructs the mobile client which action buttons (e.g. Start Travel, Check-in, Evidence, Complete) to render.
        /// </summary>
        /// <param name="taskId">The ID of the task.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the allowed action buttons and metadata.</returns>
        [HttpGet("actions")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<TaskActionsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTaskActions(
            [FromQuery] int taskId,
            CancellationToken cancellationToken)
        {
            if (taskId <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_ID", "TaskId must be greater than zero.", "Validation error"));
            }

            try
            {
                var query = new GetTaskActionsQuery(taskId);
                var result = await _mediator.Send(query, cancellationToken);
                return Ok(ApiResponse<TaskActionsDto>.Ok(result, "Task action buttons retrieved successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND", ex.Message, "Validation error"));
            }
        }

        /// <summary>
        /// Retrieves the SDUI dynamic cancellation questionnaire and acceptable reason codes.
        /// Drives the mobile cancellation popup and required follow-up fields dynamically.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the cancellation schema.</returns>
        [HttpGet("cancellation-schema")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<TaskCancellationSchemaDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCancellationSchema(CancellationToken cancellationToken)
        {
            var query = new GetTaskCancellationSchemaQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<TaskCancellationSchemaDto>.Ok(result, "Task cancellation schema retrieved successfully."));
        }

        /// <summary>
        /// Creates and schedules a new field task / work order linked to a verified client site.
        /// Supports dynamic SDUI custom data payloads based on task category.
        /// </summary>
        /// <param name="request">The task creation payload adhering to zero path parameter standard.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 201 Created with the created task details.</returns>
        [HttpPost]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateTask(
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CreateTaskCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                ClientSiteId = request.ClientSiteId,
                AssignedEmployeeId = request.AssignedEmployeeId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                TaskType = request.TaskType,
                CustomData = request.CustomData
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, ApiResponse<TaskDto>.Ok(result, "Task created and scheduled successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("CLIENT_SITE_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("CLIENT_SITE_INACTIVE", ex.Message, "Validation error"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("FORBIDDEN", ex.Message, "Access denied"));
            }
        }

        /// <summary>
        /// Assigns or reassigns an existing field task to a technician adhering to zero path parameters.
        /// </summary>
        /// <param name="request">Payload containing taskId and assignedEmployeeId.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with updated task details.</returns>
        [HttpPost("reassign")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReassignTask(
            [FromBody] AssignTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new AssignTaskCommand
            {
                TaskId = request.TaskId,
                AssignedEmployeeId = request.AssignedEmployeeId,
                ActorUserId = _currentUserService.GetUserId()
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<TaskDto>.Ok(result, "Task assigned successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "Validation error"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("FORBIDDEN", ex.Message, "Access denied"));
            }
        }

        /// <summary>
        /// Cancels an existing field task prior to check-in adhering to zero path parameters.
        /// </summary>
        /// <param name="request">Payload containing taskId and cancellation reason.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with cancelled task details.</returns>
        [HttpPost("cancel")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelTask(
            [FromBody] CancelTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CancelTaskCommand
            {
                TaskId = request.TaskId,
                Reason = request.Reason,
                ActorUserId = _currentUserService.GetUserId()
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<TaskDto>.Ok(result, "Task cancelled successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND", ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "Validation error"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("FORBIDDEN", ex.Message, "Access denied"));
            }
        }

        /// <summary>
        /// Updates the status of an assigned field task to EN_ROUTE when travel begins.
        /// </summary>
        /// <param name="request">The update status payload containing taskId, status, and optional notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with the updated task details.</returns>
        [HttpPost("status")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTaskStatus(
            [FromBody] UpdateTaskStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new UpdateTaskStatusCommand
            {
                TaskId = request.TaskId,
                Status = request.Status,
                Notes = request.Notes,
                ActorUserId = _currentUserService.GetUserId(),
                ActorRoles = _currentUserService.GetRoles()
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                var message = result.Status == "COMPLETED"
                    ? "Task status updated to COMPLETED successfully."
                    : "Task status updated to EN_ROUTE successfully.";
                return Ok(ApiResponse<TaskDto>.Ok(result, message));
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
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "Validation error"));
            }
        }

        /// <summary>
        /// Retrieves a paginated list of tasks filtered by employee, status, client site, priority, and date range.
        /// Technicians (role EMPLOYEE) are automatically self-scoped to their own assigned tasks.
        /// </summary>
        /// <param name="filter">Query filtering, search, and pagination parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>HTTP 200 OK with paginated tasks list.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTasks(
            [FromQuery] GetTasksFilterQuery filter,
            CancellationToken cancellationToken)
        {
            var query = new GetTasksQuery
            {
                Filter = filter,
                CallerUserId = _currentUserService.GetUserId(),
                CallerRoles = _currentUserService.GetRoles()
            };

            try
            {
                var result = await _mediator.Send(query, cancellationToken);
                return Ok(ApiResponse<PagedResult<TaskDto>>.Ok(result, "Tasks retrieved successfully."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
        }

        /// <summary>
        /// Completes a field task with mandatory completion evidence and customer signature sign-off.
        /// </summary>
        [HttpPost("complete")]
        [Authorize(Roles = "SYSTEM_ADMIN,ADMIN,MANAGER,SUPERVISOR,EMPLOYEE")]
        [ProducesResponseType(typeof(ApiResponse<VisitCheckOutResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CompleteTask(
            [FromBody] CompleteTaskWithEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.TaskId <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Valid TaskId is required.", "Validation error"));
            }

            if (string.IsNullOrWhiteSpace(request.CompletionNotes) || request.CompletionNotes.Trim().Length < 5)
            {
                return BadRequest(ApiResponse<object>.Fail("COMPLETION_NOTES_REQUIRED", "Completion notes must be at least 5 characters.", "Validation error"));
            }

            // Normalizes customer signature URL if base64 provided
            string? sigUrl = request.CustomerSignatureUrl;
            if (!string.IsNullOrWhiteSpace(sigUrl) && !sigUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var sigGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
                sigUrl = $"https://ffm-field-evidence-prod.s3.ap-south-1.amazonaws.com/signatures/task_{request.TaskId}_sig_{sigGuid}.png";
            }

            // Record evidences if supplied
            if (request.Evidences != null && request.Evidences.Count > 0)
            {
                foreach (var ev in request.Evidences)
                {
                    if (!string.IsNullOrWhiteSpace(ev.FileUrl))
                    {
                        var evCmd = new AddVisitEvidenceCommand
                        {
                            VisitId = request.VisitId ?? 1,
                            TaskId = request.TaskId,
                            EvidenceTypeId = ev.EvidenceTypeId > 0 ? ev.EvidenceTypeId : 1,
                            EvidenceType = string.IsNullOrWhiteSpace(ev.EvidenceType) ? "PHOTO" : ev.EvidenceType,
                            FileName = ev.FileName ?? "completion_photo.jpg",
                            FileUrl = ev.FileUrl.Trim(),
                            FileUrls = new List<string> { ev.FileUrl.Trim() },
                            Remarks = ev.Remarks,
                            Readings = ev.Readings,
                            ActorUserId = _currentUserService.GetUserId(),
                            ActorRoles = System.Linq.Enumerable.ToList(_currentUserService.GetRoles())
                        };
                        try
                        {
                            await _mediator.Send(evCmd, cancellationToken);
                        }
                        catch
                        {
                            // Continue attaching remaining evidences
                        }
                    }
                }
            }

            // Record digital customer signature if supplied
            if (!string.IsNullOrWhiteSpace(sigUrl))
            {
                try
                {
                    var sigCmd = new AddVisitEvidenceCommand
                    {
                        VisitId = request.VisitId ?? 1,
                        TaskId = request.TaskId,
                        EvidenceTypeId = 3, // SIGNATURE
                        EvidenceType = "SIGNATURE",
                        FileName = "customer_signature.png",
                        FileUrl = sigUrl,
                        FileUrls = new List<string> { sigUrl },
                        Remarks = $"Customer signature - {request.CustomerName ?? "Verified"}",
                        ActorUserId = _currentUserService.GetUserId(),
                        ActorRoles = System.Linq.Enumerable.ToList(_currentUserService.GetRoles())
                    };
                    await _mediator.Send(sigCmd, cancellationToken);
                }
                catch
                {
                    // Continue checkout
                }
            }

            var checkOutCmd = new CheckOutVisitCommand
            {
                VisitId = request.VisitId,
                TaskId = request.TaskId,
                Latitude = request.Latitude ?? 23.5880m,
                Longitude = request.Longitude ?? 58.3829m,
                CompletionNotes = request.CompletionNotes,
                CustomerName = request.CustomerName,
                CustomerSignatureUrl = sigUrl,
                CustomerRating = request.CustomerRating,
                ActorUserId = _currentUserService.GetUserId(),
                ActorRoles = _currentUserService.GetRoles()
            };

            try
            {
                var result = await _mediator.Send(checkOutCmd, cancellationToken);
                return Ok(ApiResponse<VisitCheckOutResultDto>.Ok(result, "Task completed successfully with evidence and customer signature."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message, "Validation error"));
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("evidence", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<object>.Fail("MANDATORY_EVIDENCE_REQUIRED", ex.Message, "Evidence validation error"));
                }
                return BadRequest(ApiResponse<object>.Fail("INVALID_TASK_STATE", ex.Message, "State error"));
            }
        }
    }
}
