using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Queries;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Notification.Api.Controllers;

[ApiController]
[Route("api/v1/Notifications")]
[Authorize]
public class DispatchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IValidator<SendNotificationCommand> _sendValidator;
    private readonly IValidator<MarkNotificationReadCommand> _readValidator;
    private readonly IValidator<GetNotificationHistoryQuery> _historyValidator;
    private readonly IValidator<ResendNotificationCommand> _resendValidator;
    private readonly IValidator<GetUnreadNotificationCountQuery> _unreadCountValidator;
    private readonly ICurrentUserService _currentUserService;

    public DispatchController(
        IMediator mediator,
        IValidator<SendNotificationCommand> sendValidator,
        IValidator<MarkNotificationReadCommand> readValidator,
        IValidator<GetNotificationHistoryQuery> historyValidator,
        IValidator<ResendNotificationCommand> resendValidator,
        IValidator<GetUnreadNotificationCountQuery> unreadCountValidator,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _sendValidator = sendValidator;
        _readValidator = readValidator;
        _historyValidator = historyValidator;
        _resendValidator = resendValidator;
        _unreadCountValidator = unreadCountValidator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Dispatches a push notification to a recipient employee using dynamic templates and active FCM devices.
    /// Strictly adheres to the zero path parameter standard.
    /// </summary>
    /// <param name="request">Notification dispatch payload containing recipientEmployeeId, templateCode, channel, and placeholders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with delivery status (DELIVERED, NO_ACTIVE_DEVICE, FAILED) and FCM message ID.</returns>
    [HttpPost("send")]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendNotification(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Request body cannot be null.", "Validation error"));
        }

        var command = new SendNotificationCommand
        {
            RecipientEmployeeId = request.RecipientEmployeeId,
            TemplateCode = request.TemplateCode,
            Channel = request.Channel,
            Placeholders = request.Placeholders,
            Priority = request.Priority
        };

        var validationResult = await _sendValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", firstError.ErrorMessage, "Validation error"));
        }

        try
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<SendNotificationResponse>.Ok(response, "Notification processed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail("TEMPLATE_NOT_FOUND", ex.Message, "Not Found"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("TEMPLATE_INACTIVE", ex.Message, "Invalid State"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("DISPATCH_FAILED", ex.Message, "Internal Server Error"));
        }
    }

    /// <summary>
    /// Marks one or more notifications as read, or marks all notifications as read for an employee.
    /// Strictly adheres to the zero path parameter standard.
    /// </summary>
    /// <param name="request">Payload containing notificationId, notificationIds, or recipientEmployeeId with markAll=true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with the count and IDs of notifications marked as read.</returns>
    [HttpPut("read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        [FromBody] MarkNotificationReadRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Request body cannot be null.", "Validation error"));
        }

        var command = new MarkNotificationReadCommand
        {
            NotificationId = request.NotificationId,
            NotificationIds = request.NotificationIds,
            RecipientEmployeeId = request.RecipientEmployeeId,
            MarkAll = request.MarkAll
        };

        var validationResult = await _readValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", firstError.ErrorMessage, "Validation error"));
        }

        try
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<MarkNotificationReadResponse>.Ok(response, "Notification(s) marked as read successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail("NOTIFICATION_NOT_FOUND", ex.Message, "Not Found"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("MARK_READ_FAILED", ex.Message, "Internal Server Error"));
        }
    }

    /// <summary>
    /// Retrieves paginated notification history and unread alerts using query parameters.
    /// Strictly adheres to the zero path parameter standard.
    /// </summary>
    /// <param name="request">Query options: recipientEmployeeId, isRead, status, channel, searchTerm, page, pageSize, sortBy, sortOrder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with paginated notification logs and live unreadCount.</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<NotificationHistoryPagedResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] GetNotificationHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetNotificationHistoryQuery
        {
            RecipientEmployeeId = request?.RecipientEmployeeId,
            IsRead = request?.IsRead,
            Status = request?.Status,
            Channel = request?.Channel,
            SearchTerm = request?.SearchTerm,
            Page = request?.Page ?? 1,
            PageSize = request?.PageSize ?? 15,
            SortBy = request?.SortBy ?? "dispatchedat",
            SortOrder = request?.SortOrder ?? "desc"
        };

        var validationResult = await _historyValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", firstError.ErrorMessage, "Validation error"));
        }

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<NotificationHistoryPagedResult>.Ok(response, "Notification history retrieved successfully."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("GET_HISTORY_FAILED", ex.Message, "Internal Server Error"));
        }
    }

    /// <summary>
    /// Fast counter endpoint returning live unread notifications count and critical alerts count.
    /// Strictly adheres to Zero Path Parameter standard.
    /// </summary>
    /// <param name="recipientEmployeeId">Optional target employee ID. Defaults to current user session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with unreadCount and criticalCount.</returns>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(
        [FromQuery] int? recipientEmployeeId,
        CancellationToken cancellationToken)
    {
        var employeeId = recipientEmployeeId ?? (_currentUserService.GetUserId() > 0 ? _currentUserService.GetUserId() : (int?)null);

        var query = new GetUnreadNotificationCountQuery(employeeId);
        var validationResult = await _unreadCountValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", firstError.ErrorMessage, "Validation error"));
        }

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<UnreadNotificationCountResponse>.Ok(response, "Unread notification count retrieved successfully."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("GET_UNREAD_COUNT_FAILED", ex.Message, "Internal Server Error"));
        }
    }

    /// <summary>
    /// Retries failed or un-delivered push notifications by re-querying active devices and re-dispatching via FCM.
    /// Strictly adheres to the zero path parameter standard.
    /// </summary>
    /// <param name="request">Payload containing notificationId, notificationIds, and optional force flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with detailed retry results per notification.</returns>
    [HttpPost("resend")]
    [ProducesResponseType(typeof(ApiResponse<ResendNotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendNotification(
        [FromBody] ResendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Request body cannot be null.", "Validation error"));
        }

        var command = new ResendNotificationCommand
        {
            NotificationId = request.NotificationId,
            NotificationIds = request.NotificationIds,
            Force = request.Force
        };

        var validationResult = await _resendValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", firstError.ErrorMessage, "Validation error"));
        }

        try
        {
            var response = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<ResendNotificationResponse>.Ok(response, "Notification resend processed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail("NOTIFICATION_NOT_FOUND", ex.Message, "Not Found"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("RESEND_FAILED", ex.Message, "Internal Server Error"));
        }
    }
}
