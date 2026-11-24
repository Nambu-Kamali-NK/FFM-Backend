using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Domain.Repositories;
using MediatR;

namespace FFM.Notification.Application.Queries;

public class GetUnreadNotificationCountQuery : IRequest<UnreadNotificationCountResponse>
{
    public int? RecipientEmployeeId { get; set; }

    public GetUnreadNotificationCountQuery(int? recipientEmployeeId = null)
    {
        RecipientEmployeeId = recipientEmployeeId;
    }
}

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, UnreadNotificationCountResponse>
{
    private readonly INotificationLogRepository _repository;

    public GetUnreadNotificationCountQueryHandler(INotificationLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<UnreadNotificationCountResponse> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var (unreadCount, criticalCount) = await _repository.GetUnreadCountsAsync(request.RecipientEmployeeId, cancellationToken);

        return new UnreadNotificationCountResponse
        {
            RecipientEmployeeId = request.RecipientEmployeeId,
            UnreadCount = unreadCount,
            CriticalCount = criticalCount,
            LastCheckedAt = DateTime.UtcNow
        };
    }
}
