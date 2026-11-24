using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Notification.Api.Controllers;

[ApiController]
[Route("api/v1/Notifications/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public TemplatesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Creates a new notification template for a specific event code and delivery channel.
    /// Strictly adheres to Zero Path Parameter standard.
    /// </summary>
    [HttpPost("create")]
    [Authorize(Roles = "ADMIN,SYSTEM_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Request body cannot be empty."));
        }

        var userId = _currentUserService.GetUserId();

        var command = new CreateNotificationTemplateCommand(
            request.TemplateCode,
            request.Channel,
            request.Title,
            request.BodyTemplate,
            request.Description,
            userId
        );

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<NotificationTemplateResponse>.Ok(result, "Notification template created successfully"));
        }
        catch (FluentValidation.ValidationException vex)
        {
            var errorDetail = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Validation failed", errorDetail));
        }
        catch (InvalidOperationException iex)
        {
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_TEMPLATE", iex.Message));
        }
    }

    /// <summary>
    /// Updates an existing notification template's title, body, description, or active status.
    /// </summary>
    [HttpPost("update")]
    [Authorize(Roles = "ADMIN,SYSTEM_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTemplate(
        [FromBody] UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.TemplateId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Valid TemplateId is required."));
        }

        var userId = _currentUserService.GetUserId();

        var command = new UpdateNotificationTemplateCommand(
            request.TemplateId,
            request.Title,
            request.BodyTemplate,
            request.Description,
            request.IsActive,
            userId
        );

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<NotificationTemplateResponse>.Ok(result, "Notification template updated successfully"));
        }
        catch (KeyNotFoundException knf)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", knf.Message));
        }
        catch (FluentValidation.ValidationException vex)
        {
            var errorDetail = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Validation failed", errorDetail));
        }
    }

    /// <summary>
    /// Searches or lists notification templates matching an optional common query term across all columns (code, channel, title, body, description).
    /// If query is omitted or empty, returns all notification templates.
    /// Strictly adheres to Zero Path Parameter standard.
    /// </summary>
    /// <param name="query">Optional search term matching across all template columns.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with the list of matching templates.</returns>
    [HttpGet]
    [Authorize(Roles = "ADMIN,SYSTEM_ADMIN,MANAGER")]
    [ProducesResponseType(typeof(ApiResponse<List<NotificationTemplateResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchTemplates(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = new SearchNotificationTemplatesQuery(query);
        var result = await _mediator.Send(searchQuery, cancellationToken);
        return Ok(ApiResponse<List<NotificationTemplateResponse>>.Ok(result, "Templates retrieved successfully."));
    }
}

