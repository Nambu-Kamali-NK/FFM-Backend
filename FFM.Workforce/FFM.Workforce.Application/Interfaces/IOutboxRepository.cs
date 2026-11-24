namespace FFM.Workforce.Application.Interfaces
{
    using FFM.Workforce.Domain.Entities;
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage outboxMessage);
        Task<IReadOnlyList<OutboxMessage>> GetPendingAsync();
        Task SaveChangesAsync();
        Task MarkProcessedAsync(Guid outboxMessageId);
    }
}
