using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class AddVisitEvidenceCommand : IRequest<VisitEvidenceDto>
    {
        public int VisitId { get; set; }
        public int? TaskId { get; set; }
        public int EvidenceTypeId { get; set; }
        public string EvidenceType { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? FolderPath { get; set; }
        public string? FileUrl { get; set; }
        public List<string> FileUrls { get; set; } = new();
        public List<EvidenceFileItemDto>? Files { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Remarks { get; set; }
        public decimal? CapturedLatitude { get; set; }
        public decimal? CapturedLongitude { get; set; }
        public Dictionary<string, object>? Readings { get; set; }
        public int ActorUserId { get; set; }
        public IReadOnlyList<string>? ActorRoles { get; set; }
    }
}

