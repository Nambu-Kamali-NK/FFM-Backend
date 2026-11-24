using System;
using System.Collections.Generic;

namespace FFM.Notification.Application.DTOs;

/// <summary>
/// Query parameters for retrieving notification history (Zero Path Parameters).
/// </summary>
public class GetNotificationHistoryRequest
{
    public int? RecipientEmployeeId { get; set; }
    public bool? IsRead { get; set; }
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string SortBy { get; set; } = "dispatchedat";
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Projection DTO for a single notification log record.
/// </summary>
public class NotificationLogDto
{
    public int Id { get; set; }
    public int RecipientEmployeeId { get; set; }
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string? Title { get; set; }
    public string Body { get; set; } = default!;
    public string? FcmMessageId { get; set; }
    public string Status { get; set; } = default!;
    public string? ErrorMessage { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime DispatchedAt { get; set; }
}

/// <summary>
/// Paginated envelope with live unread counter for in-app alert badges.
/// </summary>
public class NotificationHistoryPagedResult
{
    public IReadOnlyList<NotificationLogDto> Items { get; set; } = new List<NotificationLogDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 15));
    public int UnreadCount { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
