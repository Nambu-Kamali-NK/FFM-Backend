using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using FFM.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.Notification.Infrastructure.Repositories;

public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly NotificationDbContext _context;

    public NotificationTemplateRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetByCodeAndChannelAsync(string code, string channel, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.TemplateCode == code && t.Channel == channel, cancellationToken);
    }

    public async Task<NotificationTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<List<NotificationTemplate>> GetAllAsync(string? channel = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(channel))
        {
            var normalizedChannel = channel.Trim().ToUpperInvariant();
            query = query.Where(t => t.Channel == normalizedChannel);
        }

        if (onlyActive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderBy(t => t.TemplateCode).ToListAsync(cancellationToken);
    }

    public async Task<List<NotificationTemplate>> SearchAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var q = _context.NotificationTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(t =>
                t.TemplateCode.ToLower().Contains(term) ||
                t.Channel.ToLower().Contains(term) ||
                (t.Title != null && t.Title.ToLower().Contains(term)) ||
                t.BodyTemplate.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        return await q.OrderBy(t => t.TemplateCode).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        await _context.NotificationTemplates.AddAsync(template, cancellationToken);
    }

    public Task UpdateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        _context.NotificationTemplates.Update(template);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

