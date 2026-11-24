using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Models;

namespace FFM.FieldOperations.Domain.Interfaces
{
    public interface ITaskRepository
    {
        Task<string> GenerateNextTaskNumberAsync(CancellationToken cancellationToken = default);
        Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);
        Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<TaskItem?> GetByTaskNumberAsync(string taskNumber, CancellationToken cancellationToken = default);
        Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedTasksAsync(TaskFilterParameters parameters, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
