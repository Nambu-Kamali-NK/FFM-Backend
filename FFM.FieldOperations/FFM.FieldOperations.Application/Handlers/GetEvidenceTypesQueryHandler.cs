using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetEvidenceTypesQueryHandler : IRequestHandler<GetEvidenceTypesQuery, IReadOnlyList<EvidenceTypeDto>>
    {
        private readonly IEvidenceTypeRepository _evidenceTypeRepository;

        public GetEvidenceTypesQueryHandler(IEvidenceTypeRepository evidenceTypeRepository)
        {
            _evidenceTypeRepository = evidenceTypeRepository;
        }

        public async Task<IReadOnlyList<EvidenceTypeDto>> Handle(GetEvidenceTypesQuery request, CancellationToken cancellationToken)
        {
            var types = await _evidenceTypeRepository.GetAllActiveAsync(cancellationToken);
            return types.Select(t => new EvidenceTypeDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                WidgetType = t.WidgetType,
                IsActive = t.IsActive
            }).ToList();
        }
    }
}

