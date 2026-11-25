using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FFM.Workforce.Domain.Entities;

[Table("Visits")]
public class VisitEntity
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public decimal CheckInLatitude { get; set; }
    public decimal CheckInLongitude { get; set; }
    public decimal CheckInDistanceMeters { get; set; }
    public bool IsWithinGeofence { get; set; } = true;
    public DateTime? CheckOutTime { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public decimal? DurationMinutes { get; set; }
    public string? CompletionNotes { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerSignatureUrl { get; set; }
    public int? CustomerRating { get; set; }
    public string Status { get; set; } = "CHECKED_IN";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<VisitEvidenceEntity> Evidences { get; set; } = new();
}

[Table("VisitEvidences")]
public class VisitEvidenceEntity
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int EvidenceTypeId { get; set; } = 1;
    public string EvidenceType { get; set; } = "PHOTO";
    public string? FileName { get; set; }
    public string? FolderPath { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Remarks { get; set; }
    public decimal? CapturedLatitude { get; set; }
    public decimal? CapturedLongitude { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string? ReadingsJson { get; set; }
}

[Table("EvidenceTypes")]
public class EvidenceTypeEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string WidgetType { get; set; } = "PHOTO";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
