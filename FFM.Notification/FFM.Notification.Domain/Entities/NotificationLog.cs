using System;

namespace FFM.Notification.Domain.Entities;

public class NotificationLog
{
    public int Id { get; private set; }
    public int RecipientEmployeeId { get; private set; }
    public string TemplateCode { get; private set; } = default!;
    public string Channel { get; private set; } = default!; // PUSH, SMS, EMAIL, IN_APP
    public string? Title { get; private set; }
    public string Body { get; private set; } = default!;
    public string? FcmMessageId { get; private set; }
    public string Status { get; private set; } = default!; // DELIVERED, FAILED, NO_ACTIVE_DEVICE
    public string? ErrorMessage { get; private set; }
    public DateTime DispatchedAt { get; private set; }
    public bool IsRead { get; private set; } = false;
    public DateTime? ReadAt { get; private set; }

    // EF Core constructor
    protected NotificationLog() { }

    public NotificationLog(
        int recipientEmployeeId,
        string templateCode,
        string channel,
        string? title,
        string body,
        string? fcmMessageId,
        string status,
        string? errorMessage = null,
        bool isRead = false,
        DateTime? readAt = null)
    {
        RecipientEmployeeId = recipientEmployeeId;
        TemplateCode = templateCode.ToUpperInvariant();
        Channel = channel.ToUpperInvariant();
        Title = title;
        Body = body;
        FcmMessageId = fcmMessageId;
        Status = status.ToUpperInvariant();
        ErrorMessage = errorMessage;
        DispatchedAt = DateTime.UtcNow;
        IsRead = isRead;
        ReadAt = readAt;
    }

    public void MarkAsRead(DateTime? readAt = null)
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = readAt ?? DateTime.UtcNow;
        }
    }

    public void MarkResent(string status, string? fcmMessageId, string? errorMessage, DateTime? dispatchedAt = null)
    {
        Status = status.ToUpperInvariant();
        FcmMessageId = fcmMessageId;
        ErrorMessage = errorMessage;
        DispatchedAt = dispatchedAt ?? DateTime.UtcNow;
    }

    public static NotificationLog CreateDelivered(
        int recipientEmployeeId,
        string templateCode,
        string channel,
        string? title,
        string body,
        string fcmMessageId)
    {
        return new NotificationLog(recipientEmployeeId, templateCode, channel, title, body, fcmMessageId, "DELIVERED");
    }

    public static NotificationLog CreateFailed(
        int recipientEmployeeId,
        string templateCode,
        string channel,
        string? title,
        string body,
        string errorMessage)
    {
        return new NotificationLog(recipientEmployeeId, templateCode, channel, title, body, null, "FAILED", errorMessage);
    }

    public static NotificationLog CreateNoActiveDevice(
        int recipientEmployeeId,
        string templateCode,
        string channel,
        string? title,
        string body)
    {
        return new NotificationLog(recipientEmployeeId, templateCode, channel, title, body, null, "NO_ACTIVE_DEVICE", "Recipient employee has no active registered device.");
    }
}

