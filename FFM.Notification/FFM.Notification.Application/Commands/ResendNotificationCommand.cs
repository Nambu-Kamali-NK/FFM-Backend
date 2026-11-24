using System.Collections.Generic;
using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Commands;

public class ResendNotificationCommand : IRequest<ResendNotificationResponse>
{
    public int? NotificationId { get; set; }
    public List<int>? NotificationIds { get; set; }
    public bool Force { get; set; } = false;
}
