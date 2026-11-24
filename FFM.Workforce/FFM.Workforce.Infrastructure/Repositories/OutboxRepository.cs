using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FFM.Workforce.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly WorkforceDbContext _dbContext;

    public OutboxRepository(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage outboxMessage)
    {
        await _dbContext.OutboxMessages.AddAsync(outboxMessage);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync()
    {
        return await _dbContext.OutboxMessages
            .Where(o => !o.Processed)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkProcessedAsync(Guid outboxMessageId)
    {
        var message = await _dbContext.OutboxMessages.FirstOrDefaultAsync(o => o.Id == outboxMessageId);
        if (message != null)
        {
            message.MarkProcessed();
        }
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
