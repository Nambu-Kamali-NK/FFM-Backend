using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Queries;

public record GetTemplateByCodeQuery(string Code, string Channel) : IRequest<NotificationTemplateResponse?>;

public record ListNotificationTemplatesQuery(string? Channel = null, bool OnlyActive = false) : IRequest<List<NotificationTemplateResponse>>;

public record SearchNotificationTemplatesQuery(string? Query = null) : IRequest<List<NotificationTemplateResponse>>;

public record RenderTemplateQuery(
    string TemplateCode,
    string Channel,
    Dictionary<string, string> Placeholders
) : IRequest<RenderedTemplateResponse?>;

