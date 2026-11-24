using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Domain.Entities;

namespace FFM.Notification.Domain.Repositories;

public interface INotificationLogRepository
{
    Task<NotificationLog> AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationLog>> GetByRecipientAsync(int recipientEmployeeId, int take = 50, CancellationToken cancellationToken = default);
    Task<NotificationLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationLog>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationLog>> GetUnreadByRecipientAsync(int recipientEmployeeId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<NotificationLog> Items, int TotalCount, int UnreadCount)> GetHistoryPagedAsync(
        int? recipientEmployeeId,
        bool? isRead,
        string? status,
        string? channel,
        string? searchTerm,
        int page,
        int pageSize,
        string sortBy,
        string sortOrder,
        CancellationToken cancellationToken = default);
    Task<(int UnreadCount, int CriticalCount)> GetUnreadCountsAsync(int? recipientEmployeeId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

