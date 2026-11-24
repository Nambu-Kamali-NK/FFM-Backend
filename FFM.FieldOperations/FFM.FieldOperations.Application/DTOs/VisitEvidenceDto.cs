using System;
using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class VisitEvidenceDto
    {
        public int Id { get; set; }
        public int VisitId { get; set; }
        public int EvidenceTypeId { get; set; }
        public string EvidenceType { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? FolderPath { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public List<string> FileUrls { get; set; } = new();
        public IReadOnlyList<EvidenceFileItemDto>? Files { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Remarks { get; set; }
        public decimal? CapturedLatitude { get; set; }
        public decimal? CapturedLongitude { get; set; }
        public DateTime CapturedAt { get; set; }
        public Dictionary<string, object>? Readings { get; set; }
        public IReadOnlyList<VisitEvidenceDto> Items { get; set; } = new List<VisitEvidenceDto>();
    }
}

