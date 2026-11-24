using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;

namespace FFM.FieldOperations.Domain.Interfaces
{
    public interface IVisitRepository
    {
        Task<Visit?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Visit?> GetByTaskIdAsync(int taskId, CancellationToken cancellationToken = default);
        Task<Visit?> GetActiveVisitByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
        Task<Visit> AddVisitAsync(Visit visit, CancellationToken cancellationToken = default);
        Task UpdateVisitAsync(Visit visit, CancellationToken cancellationToken = default);
        Task<VisitEvidence> AddEvidenceAsync(VisitEvidence evidence, CancellationToken cancellationToken = default);
        async Task<IReadOnlyList<VisitEvidence>> AddEvidencesBatchAsync(IEnumerable<VisitEvidence> evidences, CancellationToken cancellationToken = default)
        {
            var list = new List<VisitEvidence>();
            foreach (var e in evidences)
            {
                list.Add(await AddEvidenceAsync(e, cancellationToken));
            }
            return list;
        }
        Task<IReadOnlyList<VisitEvidence>> GetEvidencesByVisitIdAsync(int visitId, string? evidenceType = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VisitEvidence>> GetEvidencesByTaskIdAsync(int taskId, string? evidenceType = null, CancellationToken cancellationToken = default);
    }
}

