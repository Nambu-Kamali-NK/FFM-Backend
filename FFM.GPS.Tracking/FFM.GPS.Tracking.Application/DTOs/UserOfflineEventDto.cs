using System;

namespace FFM.GPS.Tracking.Application.DTOs;

public class UserOfflineEventDto
{
    public int UserId { get; set; }
    public decimal? LastKnownLatitude { get; set; }
    public decimal? LastKnownLongitude { get; set; }
    public DateTime WentOfflineAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "SIGNAL_TIMEOUT";
}
