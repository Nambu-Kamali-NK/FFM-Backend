using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Queries;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class GetNotificationHistoryQueryHandler : IRequestHandler<GetNotificationHistoryQuery, NotificationHistoryPagedResult>
{
    private readonly INotificationLogRepository _logRepository;
    private readonly ILogger<GetNotificationHistoryQueryHandler> _logger;

    public GetNotificationHistoryQueryHandler(
        INotificationLogRepository logRepository,
        ILogger<GetNotificationHistoryQueryHandler> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
    }

    public async Task<NotificationHistoryPagedResult> Handle(GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount, unreadCount) = await _logRepository.GetHistoryPagedAsync(
            request.RecipientEmployeeId,
            request.IsRead,
            request.Status,
            request.Channel,
            request.SearchTerm,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortOrder,
            cancellationToken);

        var dtos = items.Select(log => new NotificationLogDto
        {
            Id = log.Id,
            RecipientEmployeeId = log.RecipientEmployeeId,
            TemplateCode = log.TemplateCode,
            Channel = log.Channel,
            Title = log.Title,
            Body = log.Body,
            FcmMessageId = log.FcmMessageId,
            Status = log.Status,
            ErrorMessage = log.ErrorMessage,
            IsRead = log.IsRead,
            ReadAt = log.ReadAt,
            DispatchedAt = log.DispatchedAt
        }).ToList();

        _logger.LogInformation(
            "Retrieved {Count} notification history items for recipient {RecipientId}. Total: {Total}, Unread: {Unread}",
            dtos.Count, request.RecipientEmployeeId, totalCount, unreadCount);

        return new NotificationHistoryPagedResult
        {
            Items = dtos,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };
    }
}
