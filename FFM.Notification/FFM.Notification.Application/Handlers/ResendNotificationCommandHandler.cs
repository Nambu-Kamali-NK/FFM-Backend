using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class ResendNotificationCommandHandler : IRequestHandler<ResendNotificationCommand, ResendNotificationResponse>
{
    private readonly INotificationLogRepository _logRepository;
    private readonly IEmployeeDeviceRepository _deviceRepository;
    private readonly IPushNotificationDispatcher _pushDispatcher;
    private readonly ILogger<ResendNotificationCommandHandler> _logger;

    public ResendNotificationCommandHandler(
        INotificationLogRepository logRepository,
        IEmployeeDeviceRepository deviceRepository,
        IPushNotificationDispatcher pushDispatcher,
        ILogger<ResendNotificationCommandHandler> logger)
    {
        _logRepository = logRepository;
        _deviceRepository = deviceRepository;
        _pushDispatcher = pushDispatcher;
        _logger = logger;
    }

    public async Task<ResendNotificationResponse> Handle(ResendNotificationCommand request, CancellationToken cancellationToken)
    {
        var targetIds = new HashSet<int>();
        if (request.NotificationId.HasValue && request.NotificationId.Value > 0)
        {
            targetIds.Add(request.NotificationId.Value);
        }
        if (request.NotificationIds != null)
        {
            foreach (var id in request.NotificationIds.Where(id => id > 0))
            {
                targetIds.Add(id);
            }
        }

        if (targetIds.Count == 0)
        {
            throw new ArgumentException("At least one valid notification ID must be provided.");
        }

        IReadOnlyList<NotificationLog> logs;
        if (targetIds.Count == 1)
        {
            var singleId = targetIds.First();
            var singleLog = await _logRepository.GetByIdAsync(singleId, cancellationToken);
            if (singleLog == null)
            {
                throw new KeyNotFoundException($"Notification with ID {singleId} was not found.");
            }
            logs = new List<NotificationLog> { singleLog };
        }
        else
        {
            logs = await _logRepository.GetByIdsAsync(targetIds, cancellationToken);
            if (logs.Count == 0)
            {
                throw new KeyNotFoundException($"None of the specified notifications ({string.Join(", ", targetIds)}) were found.");
            }
        }

        var results = new List<ResendNotificationItemResult>();

        foreach (var log in logs)
        {
            var previousStatus = log.Status;

            // 1. If already delivered and force is false, skip re-dispatching
            if (string.Equals(log.Status, "DELIVERED", StringComparison.OrdinalIgnoreCase) && !request.Force)
            {
                _logger.LogInformation("Notification {Id} is already DELIVERED and force is false. Skipping re-dispatch.", log.Id);
                results.Add(new ResendNotificationItemResult
                {
                    NotificationId = log.Id,
                    RecipientEmployeeId = log.RecipientEmployeeId,
                    PreviousStatus = previousStatus,
                    CurrentStatus = log.Status,
                    FcmMessageId = log.FcmMessageId,
                    Success = true,
                    ErrorMessage = "Notification already delivered. Skipped re-dispatch (pass force=true to override).",
                    DispatchedAt = log.DispatchedAt
                });
                continue;
            }

            // 2. Fetch recipient employee's latest active registered device
            var activeDevice = await _deviceRepository.GetActiveByEmployeeIdAsync(log.RecipientEmployeeId, cancellationToken);

            if (activeDevice == null)
            {
                _logger.LogWarning("No active registered device found for recipient employee {EmployeeId} while retrying notification {Id}.",
                    log.RecipientEmployeeId, log.Id);

                log.MarkResent("NO_ACTIVE_DEVICE", null, "Recipient employee has no active registered device.");

                results.Add(new ResendNotificationItemResult
                {
                    NotificationId = log.Id,
                    RecipientEmployeeId = log.RecipientEmployeeId,
                    PreviousStatus = previousStatus,
                    CurrentStatus = "NO_ACTIVE_DEVICE",
                    FcmMessageId = null,
                    Success = false,
                    ErrorMessage = "Recipient employee has no active registered device.",
                    DispatchedAt = log.DispatchedAt
                });
                continue;
            }

            // 3. Dispatch to active device via push dispatcher
            var customData = new Dictionary<string, string>
            {
                { "templateCode", log.TemplateCode },
                { "recipientEmployeeId", log.RecipientEmployeeId.ToString() },
                { "isResend", "true" },
                { "dispatchedAt", DateTime.UtcNow.ToString("O") }
            };

            var dispatchResult = await _pushDispatcher.SendAsync(
                activeDevice.FcmToken,
                log.Title ?? "FFM Alert",
                log.Body,
                customData,
                cancellationToken);

            if (dispatchResult.Success)
            {
                _logger.LogInformation("Push notification {Id} successfully resent to employee {EmployeeId}. MessageId: {MessageId}",
                    log.Id, log.RecipientEmployeeId, dispatchResult.MessageId);

                log.MarkResent("DELIVERED", dispatchResult.MessageId ?? "unknown", null);

                results.Add(new ResendNotificationItemResult
                {
                    NotificationId = log.Id,
                    RecipientEmployeeId = log.RecipientEmployeeId,
                    PreviousStatus = previousStatus,
                    CurrentStatus = "DELIVERED",
                    FcmMessageId = log.FcmMessageId,
                    Success = true,
                    ErrorMessage = null,
                    DispatchedAt = log.DispatchedAt
                });
            }
            else
            {
                _logger.LogError("Push notification {Id} resend failed for employee {EmployeeId}: {Error}",
                    log.Id, log.RecipientEmployeeId, dispatchResult.ErrorMessage);

                log.MarkResent("FAILED", null, dispatchResult.ErrorMessage ?? "Dispatch failed.");

                results.Add(new ResendNotificationItemResult
                {
                    NotificationId = log.Id,
                    RecipientEmployeeId = log.RecipientEmployeeId,
                    PreviousStatus = previousStatus,
                    CurrentStatus = "FAILED",
                    FcmMessageId = null,
                    Success = false,
                    ErrorMessage = log.ErrorMessage,
                    DispatchedAt = log.DispatchedAt
                });
            }
        }

        await _logRepository.SaveChangesAsync(cancellationToken);

        return new ResendNotificationResponse
        {
            TotalProcessed = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };
    }
}
