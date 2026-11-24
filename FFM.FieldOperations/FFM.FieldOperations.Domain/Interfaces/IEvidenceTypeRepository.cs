using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;

namespace FFM.FieldOperations.Domain.Interfaces
{
    public interface IEvidenceTypeRepository
    {
        Task<EvidenceType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<EvidenceType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EvidenceType>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    }
}

