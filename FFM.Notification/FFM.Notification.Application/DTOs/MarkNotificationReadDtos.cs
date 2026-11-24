using System;
using System.Collections.Generic;

namespace FFM.Notification.Application.DTOs;

/// <summary>
/// Request payload for marking notification(s) as read.
/// Strictly adheres to the zero path parameter standard.
/// </summary>
public class MarkNotificationReadRequest
{
    /// <summary>
    /// Optional: Single notification ID to mark as read.
    /// </summary>
    public int? NotificationId { get; set; }

    /// <summary>
    /// Optional: Batch list of notification IDs to mark as read.
    /// </summary>
    public List<int>? NotificationIds { get; set; }

    /// <summary>
    /// Optional: Recipient employee identifier (required when MarkAll is true).
    /// </summary>
    public int? RecipientEmployeeId { get; set; }

    /// <summary>
    /// Optional: If true, marks all unread notifications for RecipientEmployeeId as read.
    /// </summary>
    public bool MarkAll { get; set; } = false;
}

/// <summary>
/// Response payload indicating how many notifications transitioned to read.
/// </summary>
public class MarkNotificationReadResponse
{
    /// <summary>
    /// Number of notification records updated.
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// UTC timestamp when read was applied.
    /// </summary>
    public DateTime ReadAt { get; set; }

    /// <summary>
    /// List of notification IDs that were updated.
    /// </summary>
    public List<int> UpdatedIds { get; set; } = new();
}
