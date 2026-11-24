using System.Collections.Generic;
using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Commands;

public class MarkNotificationReadCommand : IRequest<MarkNotificationReadResponse>
{
    public int? NotificationId { get; set; }
    public List<int>? NotificationIds { get; set; }
    public int? RecipientEmployeeId { get; set; }
    public bool MarkAll { get; set; } = false;
}
