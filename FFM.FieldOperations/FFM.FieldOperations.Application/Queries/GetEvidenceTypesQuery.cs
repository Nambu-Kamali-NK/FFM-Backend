using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class EvidenceTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string WidgetType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class GetEvidenceTypesQuery : IRequest<IReadOnlyList<EvidenceTypeDto>>
    {
    }
}

