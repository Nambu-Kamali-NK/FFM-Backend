using System;

namespace FFM.GPS.Tracking.Application.DTOs;

public class BreadcrumbResponseDto
{
    public long BreadcrumbId { get; set; }
    public int UserId { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}
