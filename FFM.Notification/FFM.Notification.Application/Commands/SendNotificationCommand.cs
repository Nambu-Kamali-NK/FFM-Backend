using System.Collections.Generic;
using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Commands;

public class SendNotificationCommand : IRequest<SendNotificationResponse>
{
    public int RecipientEmployeeId { get; set; }
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = "PUSH";
    public Dictionary<string, string>? Placeholders { get; set; }
    public string? Priority { get; set; } = "HIGH";
}

