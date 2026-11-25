using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
[AllowAnonymous]
public class NotificationsController : ControllerBase
{
    // Thread-safe in-memory store for broadcast history & simulated device tokens
    private static readonly ConcurrentBag<NotificationHistoryItem> _history = new(new[]
    {
        new NotificationHistoryItem
        {
            Id = "NOTIF-9821",
            Title = "Emergency Water Main Leak - Mutrah",
            Message = "Immediate dispatch required at Mutrah Corniche sector. Valve isolation team deployed.",
            Priority = "CRITICAL",
            TargetType = "ZONE_BROADCAST",
            Target = "Zone Mutrah-01",
            Channels = new[] { "PUSH", "SMS", "IN_APP" },
            RecipientCount = 14,
            DeliveredCount = 14,
            FailedCount = 0,
            Status = "DELIVERED",
            CreatedAt = DateTime.UtcNow.AddMinutes(-25)
        },
        new NotificationHistoryItem
        {
            Id = "NOTIF-9820",
            Title = "Shift Schedule Published: Week 48",
            Message = "Your roster for November 25-30 has been finalized. Review your assigned municipal wards.",
            Priority = "NORMAL",
            TargetType = "ALL_FIELD_TECH",
            Target = "All Active Field Techs",
            Channels = new[] { "PUSH", "IN_APP" },
            RecipientCount = 48,
            DeliveredCount = 47,
            FailedCount = 1,
            Status = "DELIVERED",
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        },
        new NotificationHistoryItem
        {
            Id = "NOTIF-9819",
            Title = "Geofence Perimeter Breach Warning",
            Message = "Technician MCT-TEC-014 exited authorized operational boundary in Bawshar sector.",
            Priority = "HIGH",
            TargetType = "SUPERVISORS",
            Target = "Operations Command",
            Channels = new[] { "PUSH", "IN_APP" },
            RecipientCount = 5,
            DeliveredCount = 5,
            FailedCount = 0,
            Status = "DELIVERED",
            CreatedAt = DateTime.UtcNow.AddHours(-6)
        },
        new NotificationHistoryItem
        {
            Id = "NOTIF-9818",
            Title = "Safety Advisory: High Heat Index",
            Message = "Hydration mandatory. Take scheduled 15-minute shaded breaks during 12:00 - 15:00 hours.",
            Priority = "HIGH",
            TargetType = "ALL_FIELD_TECH",
            Target = "All Field Staff",
            Channels = new[] { "PUSH", "SMS" },
            RecipientCount = 62,
            DeliveredCount = 62,
            FailedCount = 0,
            Status = "DELIVERED",
            CreatedAt = DateTime.UtcNow.AddHours(-28)
        }
    });

    private static readonly List<NotificationTemplateDto> _templates = new()
    {
        new NotificationTemplateDto
        {
            Id = "TMPL-001",
            Name = "Critical Infrastructure Emergency",
            Category = "DISPATCH",
            Priority = "CRITICAL",
            DefaultTitle = "EMERGENCY: Immediate Field Response Needed",
            DefaultMessage = "Urgent incident reported at {SiteName}. All available technicians in sector report to dispatch coordinator.",
            Channels = new[] { "PUSH", "SMS", "IN_APP" }
        },
        new NotificationTemplateDto
        {
            Id = "TMPL-002",
            Name = "Task Assignment & Reallocation",
            Category = "WORK_ORDER",
            Priority = "HIGH",
            DefaultTitle = "New Priority Work Order Assigned",
            DefaultMessage = "You have been assigned Task #{TaskId} at {SiteName}. Check-in required within 30 minutes.",
            Channels = new[] { "PUSH", "IN_APP" }
        },
        new NotificationTemplateDto
        {
            Id = "TMPL-003",
            Name = "Geofence Alert / Boundary Warning",
            Category = "COMPLIANCE",
            Priority = "HIGH",
            DefaultTitle = "Geofence Boundary Alert",
            DefaultMessage = "Vehicle or device is outside authorized municipal zone ({ZoneName}). Return immediately.",
            Channels = new[] { "PUSH", "IN_APP" }
        },
        new NotificationTemplateDto
        {
            Id = "TMPL-004",
            Name = "Shift Reminder & Muster Call",
            Category = "ROSTER",
            Priority = "NORMAL",
            DefaultTitle = "Upcoming Shift Attendance Reminder",
            DefaultMessage = "Your scheduled shift begins at {ShiftStartTime}. Ensure GPS tracking is enabled upon check-in.",
            Channels = new[] { "PUSH" }
        }
    };

    /// <summary>
    /// Searches or retrieves all notification templates.
    /// </summary>
    [HttpGet("templates")]
    public IActionResult GetTemplates([FromQuery] string? query = null)
    {
        var result = string.IsNullOrWhiteSpace(query)
            ? _templates
            : _templates.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.DefaultTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(new
        {
            success = true,
            total = result.Count,
            data = result
        });
    }

    /// <summary>
    /// Sends a broadcast or targeted push notification.
    /// </summary>
    [HttpPost("send")]
    public IActionResult SendNotification([FromBody] SendNotificationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, message = "Title and message are required." });
        }

        var newId = $"NOTIF-{Random.Shared.Next(1000, 9999)}";
        var channels = request.Channels != null && request.Channels.Length > 0
            ? request.Channels
            : new[] { "PUSH", "IN_APP" };

        var recipientCount = request.TargetType switch
        {
            "ALL_FIELD_TECH" => 54,
            "SUPERVISORS" => 8,
            "ZONE_BROADCAST" => 16,
            _ => 1
        };

        var historyItem = new NotificationHistoryItem
        {
            Id = newId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "NORMAL" : request.Priority.ToUpperInvariant(),
            TargetType = string.IsNullOrWhiteSpace(request.TargetType) ? "ALL_FIELD_TECH" : request.TargetType,
            Target = string.IsNullOrWhiteSpace(request.Target) ? "All Active Technicians" : request.Target,
            Channels = channels,
            RecipientCount = recipientCount,
            DeliveredCount = recipientCount,
            FailedCount = 0,
            Status = "DELIVERED",
            CreatedAt = DateTime.UtcNow
        };

        _history.Add(historyItem);

        return Ok(new
        {
            success = true,
            messageId = newId,
            status = "DELIVERED",
            recipientCount,
            deliveredCount = recipientCount,
            dispatchedAt = historyItem.CreatedAt,
            channels = historyItem.Channels
        });
    }

    /// <summary>
    /// Retrieves notification broadcast and audit logs.
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] string? priority = null, [FromQuery] int limit = 20)
    {
        var items = _history.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(priority) && priority != "ALL")
        {
            items = items.Where(i => string.Equals(i.Priority, priority, StringComparison.OrdinalIgnoreCase));
        }

        var list = items
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            success = true,
            total = list.Count,
            data = list
        });
    }

    /// <summary>
    /// Retrieves total unread notification count.
    /// </summary>
    [HttpGet("unread-count")]
    public IActionResult GetUnreadCount()
    {
        return Ok(new
        {
            success = true,
            unreadCount = 3,
            urgentCount = 1,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Retrieves active registered push devices with FCM tokens.
    /// </summary>
    [HttpGet("devices/active")]
    public IActionResult GetActiveDevices()
    {
        var activeDevices = new[]
        {
            new { deviceId = "FCM-DEV-001", employeeCode = "MCT-TEC-010", employeeName = "Nambu Kamali", os = "Android 14", tokenPreview = "eXZ...k9Q", status = "ONLINE", lastActive = DateTime.UtcNow.AddMinutes(-2) },
            new { deviceId = "FCM-DEV-002", employeeCode = "MCT-TEC-011", employeeName = "Tariq Al-Balushi", os = "iOS 17.5", tokenPreview = "fQ1...m8Z", status = "ONLINE", lastActive = DateTime.UtcNow.AddMinutes(-5) },
            new { deviceId = "FCM-DEV-003", employeeCode = "MCT-TEC-012", employeeName = "Said Al-Habsi", os = "Android 14", tokenPreview = "aL3...x2K", status = "ONLINE", lastActive = DateTime.UtcNow.AddMinutes(-12) },
            new { deviceId = "FCM-DEV-004", employeeCode = "MCT-TEC-013", employeeName = "Ahmed Al-Kharusi", os = "Android 13", tokenPreview = "pW9...r4T", status = "IDLE", lastActive = DateTime.UtcNow.AddHours(-1) },
            new { deviceId = "FCM-DEV-005", employeeCode = "MCT-TEC-014", employeeName = "Rashid Al-Maawali", os = "Android 14", tokenPreview = "cK8...v1Y", status = "ONLINE", lastActive = DateTime.UtcNow.AddMinutes(-1) }
        };

        return Ok(new
        {
            success = true,
            total = activeDevices.Length,
            data = activeDevices
        });
    }

    /// <summary>
    /// Registers or updates an FCM token for a device.
    /// </summary>
    [HttpPost("devices/register")]
    public IActionResult RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { success = false, message = "Device token is required." });
        }

        return Ok(new
        {
            success = true,
            registered = true,
            deviceId = request.DeviceId ?? $"DEV-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            registeredAt = DateTime.UtcNow
        });
    }
}

public class SendNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "NORMAL";
    public string TargetType { get; set; } = "ALL_FIELD_TECH";
    public string? Target { get; set; }
    public string[]? Channels { get; set; }
}

public class RegisterDeviceRequest
{
    public string? DeviceId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? EmployeeCode { get; set; }
}

public class NotificationHistoryItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "NORMAL";
    public string TargetType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string[] Channels { get; set; } = Array.Empty<string>();
    public int RecipientCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }
    public string Status { get; set; } = "DELIVERED";
    public DateTime CreatedAt { get; set; }
}

public class NotificationTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = "NORMAL";
    public string DefaultTitle { get; set; } = string.Empty;
    public string DefaultMessage { get; set; } = string.Empty;
    public string[] Channels { get; set; } = Array.Empty<string>();
}
