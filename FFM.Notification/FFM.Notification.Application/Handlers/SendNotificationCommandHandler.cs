using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Services;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, SendNotificationResponse>
{
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IEmployeeDeviceRepository _deviceRepository;
    private readonly INotificationLogRepository _logRepository;
    private readonly ITemplateInterpolationEngine _interpolationEngine;
    private readonly IPushNotificationDispatcher _pushDispatcher;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    public SendNotificationCommandHandler(
        INotificationTemplateRepository templateRepository,
        IEmployeeDeviceRepository deviceRepository,
        INotificationLogRepository logRepository,
        ITemplateInterpolationEngine interpolationEngine,
        IPushNotificationDispatcher pushDispatcher,
        ILogger<SendNotificationCommandHandler> logger)
    {
        _templateRepository = templateRepository;
        _deviceRepository = deviceRepository;
        _logRepository = logRepository;
        _interpolationEngine = interpolationEngine;
        _pushDispatcher = pushDispatcher;
        _logger = logger;
    }

    public async Task<SendNotificationResponse> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.TemplateCode.Trim().ToUpperInvariant();
        var normalizedChannel = request.Channel.Trim().ToUpperInvariant();

        // 1. Fetch template
        var template = await _templateRepository.GetByCodeAndChannelAsync(normalizedCode, normalizedChannel, cancellationToken);
        if (template == null)
        {
            throw new KeyNotFoundException($"Notification template '{normalizedCode}' for channel '{normalizedChannel}' was not found.");
        }

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Notification template '{normalizedCode}' is currently inactive.");
        }

        // 2. Interpolate title and body
        var placeholders = request.Placeholders ?? new Dictionary<string, string>();
        var renderedTitle = template.Title != null
            ? _interpolationEngine.Interpolate(template.Title, placeholders)
            : null;
        var renderedBody = _interpolationEngine.Interpolate(template.BodyTemplate, placeholders);

        // 3. Resolve active recipient device
        var activeDevice = await _deviceRepository.GetActiveByEmployeeIdAsync(request.RecipientEmployeeId, cancellationToken);

        if (activeDevice == null)
        {
            _logger.LogWarning("No active registered device found for recipient employee {EmployeeId}. Notification marked NO_ACTIVE_DEVICE.", request.RecipientEmployeeId);
            var noDeviceLog = NotificationLog.CreateNoActiveDevice(
                request.RecipientEmployeeId,
                template.TemplateCode,
                template.Channel,
                renderedTitle,
                renderedBody);

            await _logRepository.AddAsync(noDeviceLog, cancellationToken);

            return new SendNotificationResponse
            {
                NotificationLogId = noDeviceLog.Id,
                RecipientEmployeeId = request.RecipientEmployeeId,
                TemplateCode = template.TemplateCode,
                Channel = template.Channel,
                RenderedTitle = renderedTitle,
                RenderedBody = renderedBody,
                Status = "NO_ACTIVE_DEVICE",
                ErrorMessage = "Recipient employee has no active registered device.",
                DispatchedAt = noDeviceLog.DispatchedAt
            };
        }

        // 4. Dispatch to active device
        var customData = new Dictionary<string, string>
        {
            { "templateCode", template.TemplateCode },
            { "recipientEmployeeId", request.RecipientEmployeeId.ToString() },
            { "priority", request.Priority ?? "HIGH" },
            { "dispatchedAt", DateTime.UtcNow.ToString("O") }
        };

        foreach (var kvp in placeholders)
        {
            if (!customData.ContainsKey(kvp.Key))
            {
                customData[kvp.Key] = kvp.Value;
            }
        }

        var dispatchResult = await _pushDispatcher.SendAsync(
            activeDevice.FcmToken,
            renderedTitle ?? "FFM Alert",
            renderedBody,
            customData,
            cancellationToken);

        NotificationLog log;
        if (dispatchResult.Success)
        {
            _logger.LogInformation("Push notification successfully dispatched to employee {EmployeeId}. MessageId: {MessageId}",
                request.RecipientEmployeeId, dispatchResult.MessageId);

            log = NotificationLog.CreateDelivered(
                request.RecipientEmployeeId,
                template.TemplateCode,
                template.Channel,
                renderedTitle,
                renderedBody,
                dispatchResult.MessageId ?? "unknown");
        }
        else
        {
            _logger.LogError("Push notification failed for employee {EmployeeId}: {Error}",
                request.RecipientEmployeeId, dispatchResult.ErrorMessage);

            log = NotificationLog.CreateFailed(
                request.RecipientEmployeeId,
                template.TemplateCode,
                template.Channel,
                renderedTitle,
                renderedBody,
                dispatchResult.ErrorMessage ?? "Dispatch failed.");
        }

        await _logRepository.AddAsync(log, cancellationToken);

        return new SendNotificationResponse
        {
            NotificationLogId = log.Id,
            RecipientEmployeeId = request.RecipientEmployeeId,
            TemplateCode = template.TemplateCode,
            Channel = template.Channel,
            RenderedTitle = renderedTitle,
            RenderedBody = renderedBody,
            Status = log.Status,
            FcmMessageId = log.FcmMessageId,
            ErrorMessage = log.ErrorMessage,
            DispatchedAt = log.DispatchedAt
        };
    }
}
