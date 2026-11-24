using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Commands;

public record CreateNotificationTemplateCommand(
    string TemplateCode,
    string Channel,
    string? Title,
    string BodyTemplate,
    string? Description,
    int CreatedByUserId
) : IRequest<NotificationTemplateResponse>;

public record UpdateNotificationTemplateCommand(
    int TemplateId,
    string? Title,
    string BodyTemplate,
    string? Description,
    bool IsActive,
    int UpdatedByUserId
) : IRequest<NotificationTemplateResponse>;

