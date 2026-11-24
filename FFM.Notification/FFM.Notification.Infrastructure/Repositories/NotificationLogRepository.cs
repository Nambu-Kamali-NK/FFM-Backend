using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using FFM.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.Notification.Infrastructure.Repositories;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationDbContext _context;

    public NotificationLogRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationLog> AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        await _context.NotificationLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<IReadOnlyList<NotificationLog>> GetByRecipientAsync(int recipientEmployeeId, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .Where(l => l.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(l => l.DispatchedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationLog>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new List<NotificationLog>();

        return await _context.NotificationLogs
            .Where(l => idList.Contains(l.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationLog>> GetUnreadByRecipientAsync(int recipientEmployeeId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .Where(l => l.RecipientEmployeeId == recipientEmployeeId && !l.IsRead)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<NotificationLog> Items, int TotalCount, int UnreadCount)> GetHistoryPagedAsync(
        int? recipientEmployeeId,
        bool? isRead,
        string? status,
        string? channel,
        string? searchTerm,
        int page,
        int pageSize,
        string sortBy,
        string sortOrder,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationLogs.AsNoTracking().AsQueryable();

        if (recipientEmployeeId.HasValue)
        {
            query = query.Where(l => l.RecipientEmployeeId == recipientEmployeeId.Value);
        }

        if (isRead.HasValue)
        {
            query = query.Where(l => l.IsRead == isRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(l => l.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalizedChannel = channel.Trim().ToUpperInvariant();
            query = query.Where(l => l.Channel == normalizedChannel);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(l =>
                (l.Title != null && l.Title.ToLower().Contains(term)) ||
                l.Body.ToLower().Contains(term) ||
                l.TemplateCode.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Compute live unread count
        int unreadCount;
        if (recipientEmployeeId.HasValue)
        {
            unreadCount = await _context.NotificationLogs
                .AsNoTracking()
                .CountAsync(l => l.RecipientEmployeeId == recipientEmployeeId.Value && !l.IsRead, cancellationToken);
        }
        else
        {
            unreadCount = await _context.NotificationLogs
                .AsNoTracking()
                .CountAsync(l => !l.IsRead, cancellationToken);
        }

        // Sorting
        var isAscending = string.Equals(sortOrder, "asc", System.StringComparison.OrdinalIgnoreCase);
        var sortKey = (sortBy ?? string.Empty).Trim().ToLowerInvariant();

        query = sortKey switch
        {
            "id" => isAscending ? query.OrderBy(l => l.Id) : query.OrderByDescending(l => l.Id),
            "recipientemployeeid" => isAscending ? query.OrderBy(l => l.RecipientEmployeeId) : query.OrderByDescending(l => l.RecipientEmployeeId),
            _ => isAscending ? query.OrderBy(l => l.DispatchedAt) : query.OrderByDescending(l => l.DispatchedAt)
        };

        // Paging
        var skip = Math.Max(0, (page - 1) * pageSize);
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);

        return (items, totalCount, unreadCount);
    }

    public async Task<(int UnreadCount, int CriticalCount)> GetUnreadCountsAsync(int? recipientEmployeeId, CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationLogs.AsNoTracking().Where(l => !l.IsRead);

        if (recipientEmployeeId.HasValue && recipientEmployeeId.Value > 0)
        {
            query = query.Where(l => l.RecipientEmployeeId == recipientEmployeeId.Value);
        }

        var unreadCount = await query.CountAsync(cancellationToken);

        var criticalCount = await query.CountAsync(l =>
            l.TemplateCode.Contains("CRITICAL") ||
            l.TemplateCode.Contains("GEOFENCE") ||
            l.TemplateCode.Contains("SECURITY") ||
            (l.Title != null && l.Title.ToUpper().Contains("CRITICAL")), cancellationToken);

        return (unreadCount, criticalCount);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

