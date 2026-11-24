using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.FieldOperations.Infrastructure.Repositories
{
    public class VisitRepository : IVisitRepository
    {
        private readonly FieldOperationsDbContext _dbContext;

        public VisitRepository(FieldOperationsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Visit?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Visits
                .Include(v => v.Task)
                .Include(v => v.Evidences)
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        }

        public async Task<Visit?> GetByTaskIdAsync(int taskId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Visits
                .Include(v => v.Task)
                .Include(v => v.Evidences)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync(v => v.TaskId == taskId, cancellationToken);
        }

        public async Task<Visit?> GetActiveVisitByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Visits
                .FirstOrDefaultAsync(v => v.EmployeeId == employeeId && v.CheckOutTime == null && v.Status == "CHECKED_IN", cancellationToken);
        }

        public async Task<Visit> AddVisitAsync(Visit visit, CancellationToken cancellationToken = default)
        {
            await _dbContext.Visits.AddAsync(visit, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return visit;
        }

        public async Task UpdateVisitAsync(Visit visit, CancellationToken cancellationToken = default)
        {
            _dbContext.Visits.Update(visit);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<VisitEvidence> AddEvidenceAsync(VisitEvidence evidence, CancellationToken cancellationToken = default)
        {
            await _dbContext.VisitEvidences.AddAsync(evidence, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return evidence;
        }

        public async Task<IReadOnlyList<VisitEvidence>> AddEvidencesBatchAsync(IEnumerable<VisitEvidence> evidences, CancellationToken cancellationToken = default)
        {
            var list = evidences.ToList();
            if (!list.Any()) return list;
            await _dbContext.VisitEvidences.AddRangeAsync(list, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return list;
        }

        public async Task<IReadOnlyList<VisitEvidence>> GetEvidencesByVisitIdAsync(
            int visitId,
            string? evidenceType = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.VisitEvidences
                .AsNoTracking()
                .Where(e => e.VisitId == visitId);

            if (!string.IsNullOrWhiteSpace(evidenceType))
            {
                var normalizedType = evidenceType.Trim().ToUpperInvariant();
                query = query.Where(e => e.EvidenceType == normalizedType);
            }

            return await query
                .OrderBy(e => e.CapturedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VisitEvidence>> GetEvidencesByTaskIdAsync(
            int taskId,
            string? evidenceType = null,
            CancellationToken cancellationToken = default)
        {
            var visitIds = await _dbContext.Visits
                .AsNoTracking()
                .Where(v => v.TaskId == taskId)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);

            if (visitIds.Count == 0)
                return Array.Empty<VisitEvidence>();

            var query = _dbContext.VisitEvidences
                .AsNoTracking()
                .Where(e => visitIds.Contains(e.VisitId));

            if (!string.IsNullOrWhiteSpace(evidenceType))
            {
                var normalizedType = evidenceType.Trim().ToUpperInvariant();
                query = query.Where(e => e.EvidenceType == normalizedType);
            }

            return await query
                .OrderBy(e => e.CapturedAt)
                .ToListAsync(cancellationToken);
        }
    }
}

