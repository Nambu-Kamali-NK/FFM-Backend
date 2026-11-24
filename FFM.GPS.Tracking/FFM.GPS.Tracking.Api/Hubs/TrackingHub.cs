using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FFM.GPS.Tracking.Api.Hubs;

[Authorize]
public class TrackingHub : Hub<ITrackingClient>, ITrackingHubMethods
{
    private readonly ILogger<TrackingHub> _logger;
    private readonly IMediator? _mediator;

    public TrackingHub(ILogger<TrackingHub> logger, IMediator? mediator = null)
    {
        _logger = logger;
        _mediator = mediator;
    }


    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
            ?? Context.User?.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(userIdClaim))
        {
            // Automatically register connection to their own user channel
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userIdClaim}");
            _logger.LogInformation("Connection {ConnectionId} for User {UserId} (Role: {Role}) joined channel user_{UserId}",
                Context.ConnectionId, userIdClaim, role, userIdClaim);
        }

        // Supervisors, Admins, and Managers join the global supervisors channel
        if (!string.IsNullOrEmpty(role) && !string.Equals(role, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "supervisors");
            _logger.LogInformation("Connection {ConnectionId} (Role: {Role}) joined group 'supervisors'",
                Context.ConnectionId, role);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdClaim = Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogInformation("Connection {ConnectionId} for User {UserId} disconnected. Exception: {Error}",
                Context.ConnectionId, userIdClaim, exception?.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinTeamStream(int teamId)
    {
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
            ?? Context.User?.FindFirst("role")?.Value;

        // IDOR check: EMPLOYEES cannot listen to full team broadcast streams
        if (string.Equals(role, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Access denied: Employee {ConnectionId} attempted to join team stream {TeamId}",
                Context.ConnectionId, teamId);
            throw new HubException("Forbidden: Field employees are restricted to their individual telemetry channel.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"team_{teamId}");
        _logger.LogInformation("Connection {ConnectionId} joined team stream team_{TeamId}",
            Context.ConnectionId, teamId);
    }

    public async Task LeaveTeamStream(int teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"team_{teamId}");
        _logger.LogInformation("Connection {ConnectionId} left team stream team_{TeamId}",
            Context.ConnectionId, teamId);
    }

    public async Task TrackUser(int targetUserId)
    {
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
            ?? Context.User?.FindFirst("role")?.Value;

        var callerUserIdStr = Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        int.TryParse(callerUserIdStr, out var callerUserId);

        // IDOR check: Employees can only track themselves
        if (string.Equals(role, "EMPLOYEE", StringComparison.OrdinalIgnoreCase) && callerUserId != targetUserId)
        {
            _logger.LogWarning("Access denied: Employee {CallerUserId} attempted to track user {TargetUserId}",
                callerUserId, targetUserId);
            throw new HubException("Forbidden: Employees cannot track other users' location streams.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{targetUserId}");
        _logger.LogInformation("Connection {ConnectionId} subscribed to user channel user_{TargetUserId}",
            Context.ConnectionId, targetUserId);
    }

    public async Task UntrackUser(int targetUserId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{targetUserId}");
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from user channel user_{TargetUserId}",
            Context.ConnectionId, targetUserId);
    }

    public async Task<ApiResponse<BreadcrumbResponseDto>> SendBreadcrumb(CreateBreadcrumbRequest request)
    {
        if (request == null)
        {
            throw new HubException("Request body cannot be empty.");
        }

        if (request.Latitude < -90m || request.Latitude > 90m || request.Longitude < -180m || request.Longitude > 180m)
        {
            throw new HubException("Invalid GPS coordinates. Latitude must be between -90 and +90, Longitude between -180 and +180.");
        }

        var userRole = Context.User?.FindFirst("role")?.Value ?? Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var callerUserIdStr = Context.User?.FindFirst("userId")?.Value ?? Context.User?.FindFirst("user_id")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;

        if (string.Equals(userRole, "EMPLOYEE", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(callerUserIdStr, out var callerUserId))
            {
                if (request.UserId > 0 && request.UserId != callerUserId)
                {
                    throw new HubException("Forbidden: Employees are only permitted to record breadcrumbs for their own account.");
                }
                request.UserId = callerUserId;
            }
        }

        if (request.UserId <= 0)
        {
            throw new HubException("UserId must be a positive integer.");
        }

        if (_mediator == null)
        {
            throw new HubException("Mediator service is unavailable.");
        }

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

        _logger.LogInformation("WebSocket breadcrumb recorded for User {UserId} at ({Lat}, {Lng})",
            request.UserId, request.Latitude, request.Longitude);

        return ApiResponse<BreadcrumbResponseDto>.Ok(result, "Breadcrumb recorded successfully via WebSocket stream.");
    }
}


