using System;
using System.Collections.Generic;

namespace FFM.Notification.Application.DTOs;

/// <summary>
/// Request payload for retrying failed or undelivered push notifications.
/// Strictly adheres to Zero Path Parameter standard.
/// </summary>
public class ResendNotificationRequest
{
    public int? NotificationId { get; set; }
    public List<int>? NotificationIds { get; set; }
    public bool Force { get; set; } = false;
}

/// <summary>
/// Result status for an individual notification retry attempt.
/// </summary>
public class ResendNotificationItemResult
{
    public int NotificationId { get; set; }
    public int RecipientEmployeeId { get; set; }
    public string PreviousStatus { get; set; } = default!;
    public string CurrentStatus { get; set; } = default!;
    public string? FcmMessageId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DispatchedAt { get; set; }
}

/// <summary>
/// Envelope response for the resend endpoint.
/// </summary>
public class ResendNotificationResponse
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public IReadOnlyList<ResendNotificationItemResult> Results { get; set; } = new List<ResendNotificationItemResult>();
}
