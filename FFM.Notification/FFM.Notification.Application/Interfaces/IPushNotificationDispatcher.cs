using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FFM.Notification.Application.Interfaces;

public class PushDispatchResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DispatchedAt { get; set; } = DateTime.UtcNow;

    public static PushDispatchResult Ok(string messageId) => new()
    {
        Success = true,
        MessageId = messageId,
        DispatchedAt = DateTime.UtcNow
    };

    public static PushDispatchResult Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        DispatchedAt = DateTime.UtcNow
    };
}

public interface IPushNotificationDispatcher
{
    Task<PushDispatchResult> SendAsync(
        string fcmToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}

