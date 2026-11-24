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
    public class EvidenceTypeRepository : IEvidenceTypeRepository
    {
        private readonly FieldOperationsDbContext _context;

        public EvidenceTypeRepository(FieldOperationsDbContext context)
        {
            _context = context;
        }

        public async Task<EvidenceType?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.EvidenceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task<EvidenceType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return await _context.EvidenceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == normalized, cancellationToken);
        }

        public async Task<IReadOnlyList<EvidenceType>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.EvidenceTypes
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Id)
                .ToListAsync(cancellationToken);
        }
    }
}

