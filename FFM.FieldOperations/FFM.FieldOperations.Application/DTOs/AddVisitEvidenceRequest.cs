using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class EvidenceFileItemDto
    {
        public string? FileName { get; set; }
        public string? FolderPath { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public int EvidenceTypeId { get; set; } = 1;
        public string? ThumbnailUrl { get; set; }
        public string? Remarks { get; set; }
    }

    public class AddVisitEvidenceRequest
    {
        public int VisitId { get; set; }
        public int? TaskId { get; set; }
        public int EvidenceTypeId { get; set; } = 1;
        public string? FileName { get; set; }
        public string? FolderPath { get; set; }
        public List<string>? FileUrls { get; set; }
        public List<EvidenceFileItemDto>? Files { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Remarks { get; set; }
        public decimal? CapturedLatitude { get; set; }
        public decimal? CapturedLongitude { get; set; }
        public Dictionary<string, object>? Readings { get; set; }
    }
}

