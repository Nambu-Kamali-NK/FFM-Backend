using System;
using System.Collections.Generic;

namespace FFM.Notification.Application.DTOs;

public class SendNotificationRequest
{
    public int RecipientEmployeeId { get; set; }
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = "PUSH";
    public Dictionary<string, string>? Placeholders { get; set; }
    public string? Priority { get; set; } = "HIGH";
}

public class SendNotificationResponse
{
    public int NotificationLogId { get; set; }
    public int RecipientEmployeeId { get; set; }
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string? RenderedTitle { get; set; }
    public string RenderedBody { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? FcmMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DispatchedAt { get; set; }
}

public class GetDeviceTokenRequest
{
    public int EmployeeId { get; set; }
}

public class GetDeviceTokenResponse
{
    public int EmployeeId { get; set; }
    public string DeviceId { get; set; } = default!;
    public string Platform { get; set; } = default!;
    public string DeviceModel { get; set; } = default!;
    public string FcmToken { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime LastActiveAt { get; set; }
}

