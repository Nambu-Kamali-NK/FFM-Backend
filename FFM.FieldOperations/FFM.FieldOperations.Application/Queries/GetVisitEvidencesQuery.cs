using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetVisitEvidencesQuery : IRequest<IReadOnlyList<VisitEvidenceDto>>
    {
        public int? VisitId { get; set; }
        public int? TaskId { get; set; }
        public string? EvidenceType { get; set; }
        public int CallerUserId { get; set; }
        public IReadOnlyList<string>? CallerRoles { get; set; }
    }
}

