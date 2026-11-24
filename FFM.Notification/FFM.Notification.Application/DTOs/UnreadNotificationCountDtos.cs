using System;

namespace FFM.Notification.Application.DTOs;

/// <summary>
/// Response payload for fast unread notification counter.
/// </summary>
public class UnreadNotificationCountResponse
{
    public int? RecipientEmployeeId { get; set; }
    public int UnreadCount { get; set; }
    public int CriticalCount { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}
