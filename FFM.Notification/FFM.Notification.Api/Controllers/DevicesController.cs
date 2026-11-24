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
[Route("api/v1/Notifications/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public DevicesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Registers or updates a mobile device hardware and FCM push token for the authenticated employee.
    /// Strictly adheres to Zero Path Parameter standard.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<DeviceRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DeviceRegistrationResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Request body cannot be empty."));
        }

        var employeeId = _currentUserService.GetUserId();
        if (employeeId <= 0)
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", "Valid authenticated employee session required."));
        }

        var command = new RegisterDeviceCommand(
            employeeId,
            request.DeviceId,
            request.Platform,
            request.DeviceModel,
            request.OsVersion,
            request.AppVersion,
            request.FcmToken
        );

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<DeviceRegistrationResponse>.Ok(result, "Device registered successfully for push notifications"));
        }
        catch (FluentValidation.ValidationException vex)
        {
            var errorDetail = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Validation failed", errorDetail));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail("INTERNAL_SERVER_ERROR", ex.Message));
        }
    }

    /// <summary>
    /// De-registers a device upon user logout to suspend background push notifications.
    /// Idempotent and adheres to Zero Path Parameter standard.
    /// </summary>
    [HttpPost("deregister")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeregisterDevice(
        [FromBody] DeregisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "DeviceId is required."));
        }

        var employeeId = _currentUserService.GetUserId();
        if (employeeId <= 0)
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", "Valid authenticated employee session required."));
        }

        var command = new DeregisterDeviceCommand(employeeId, request.DeviceId);
        var success = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(success, "Device de-registered successfully. Push notifications suspended."));
    }

    /// <summary>
    /// Retrieves the current active device registration for the authenticated employee.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<DeviceRegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveDevice(CancellationToken cancellationToken)
    {
        var employeeId = _currentUserService.GetUserId();
        if (employeeId <= 0)
        {
            return Unauthorized(ApiResponse<object>.Fail("UNAUTHORIZED", "Valid authenticated employee session required."));
        }

        var query = new GetActiveDeviceQuery(employeeId);
        var result = await _mediator.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<object>.Fail("DEVICE_NOT_FOUND", "No active device found for the employee."));
        }

        return Ok(ApiResponse<DeviceRegistrationResponse>.Ok(result, "Active device retrieved successfully."));
    }

    /// <summary>
    /// Retrieves the active FCM device token for a specified employee adhering to zero path parameters.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(ApiResponse<GetDeviceTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeviceToken(
        [FromBody] GetDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.EmployeeId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("INVALID_PAYLOAD", "Valid employeeId required in request body."));
        }

        var query = new GetDeviceTokenQuery(request.EmployeeId);
        var result = await _mediator.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<object>.Fail("DEVICE_NOT_FOUND", $"No active device registration found for employee {request.EmployeeId}."));
        }

        return Ok(ApiResponse<GetDeviceTokenResponse>.Ok(result, "Device token retrieved successfully."));
    }
}

