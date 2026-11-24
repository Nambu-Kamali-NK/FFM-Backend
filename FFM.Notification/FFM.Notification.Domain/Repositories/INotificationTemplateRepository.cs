using FFM.Notification.Domain.Entities;

namespace FFM.Notification.Domain.Repositories;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByCodeAndChannelAsync(string code, string channel, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<NotificationTemplate>> GetAllAsync(string? channel = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<List<NotificationTemplate>> SearchAsync(string? query = null, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

