using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FFM.Workforce.Domain.Entities;

[Table("Tasks")]
public class FieldTaskEntity
{
    public int Id { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public string Status { get; set; } = "ASSIGNED";
    public int? ClientSiteId { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? TaskType { get; set; }
}

[Table("ClientSites")]
public class ClientSiteEntity
{
    public int Id { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double GeofenceRadiusMeters { get; set; }
    public bool IsActive { get; set; }
}
