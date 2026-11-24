using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Queries;

public class GetNotificationHistoryQuery : IRequest<NotificationHistoryPagedResult>
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
