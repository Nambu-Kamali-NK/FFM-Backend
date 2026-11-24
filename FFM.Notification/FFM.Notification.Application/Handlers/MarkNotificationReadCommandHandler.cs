using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, MarkNotificationReadResponse>
{
    private readonly INotificationLogRepository _logRepository;
    private readonly ILogger<MarkNotificationReadCommandHandler> _logger;

    public MarkNotificationReadCommandHandler(
        INotificationLogRepository logRepository,
        ILogger<MarkNotificationReadCommandHandler> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
    }

    public async Task<MarkNotificationReadResponse> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var readAt = DateTime.UtcNow;
        var updatedIds = new List<int>();

        // 1. Mark All Mode for Recipient
        if (request.MarkAll && request.RecipientEmployeeId.HasValue)
        {
            var unreadLogs = await _logRepository.GetUnreadByRecipientAsync(request.RecipientEmployeeId.Value, cancellationToken);
            foreach (var log in unreadLogs)
            {
                log.MarkAsRead(readAt);
                updatedIds.Add(log.Id);
            }

            if (updatedIds.Count > 0)
            {
                await _logRepository.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Marked {Count} notifications as read for employee {EmployeeId}", updatedIds.Count, request.RecipientEmployeeId.Value);

            return new MarkNotificationReadResponse
            {
                UpdatedCount = updatedIds.Count,
                ReadAt = readAt,
                UpdatedIds = updatedIds
            };
        }

        // 2. Specific Notification ID(s) Mode
        var targetIds = new HashSet<int>();
        if (request.NotificationId.HasValue)
        {
            targetIds.Add(request.NotificationId.Value);
        }

        if (request.NotificationIds != null)
        {
            foreach (var id in request.NotificationIds)
            {
                if (id > 0) targetIds.Add(id);
            }
        }

        var logs = await _logRepository.GetByIdsAsync(targetIds, cancellationToken);

        // If a single specific ID was requested and it doesn't exist, fail with 404
        if (request.NotificationId.HasValue && targetIds.Count == 1 && logs.Count == 0)
        {
            throw new KeyNotFoundException($"Notification with ID {request.NotificationId.Value} was not found.");
        }

        foreach (var log in logs)
        {
            if (!log.IsRead)
            {
                log.MarkAsRead(readAt);
                updatedIds.Add(log.Id);
            }
            else
            {
                // Already read: idempotent success
                updatedIds.Add(log.Id);
            }
        }

        if (updatedIds.Count > 0)
        {
            await _logRepository.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Marked {Count} notifications as read out of {Total} requested", updatedIds.Count, targetIds.Count);

        return new MarkNotificationReadResponse
        {
            UpdatedCount = updatedIds.Count,
            ReadAt = readAt,
            UpdatedIds = updatedIds
        };
    }
}
