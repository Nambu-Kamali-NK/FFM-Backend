using System;

namespace FFM.GPS.Tracking.Application.DTOs
{
    public class GeofenceBreachResponseDto
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public int GeofenceId { get; set; }
        public string GeofenceName { get; set; } = string.Empty;
        public string LocationRef { get; set; } = string.Empty;
        public string BreachType { get; set; } = string.Empty; // EXIT or ENTRY
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public double? DistanceFromBoundaryMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
        public DateTime TriggeredAt { get; set; }
        public bool IsAcknowledged { get; set; }
        public string? AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PagedBreachesResultDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
        public System.Collections.Generic.List<GeofenceBreachResponseDto> Items { get; set; } = new();
    }
}
