using System.Threading;
using System.Threading.Tasks;

namespace FFM.FieldOperations.Application.Interfaces
{
    public interface ITaskNotificationDispatcher
    {
        Task DispatchTaskAssignmentNotificationAsync(
            int taskId,
            string taskNumber,
            string taskTitle,
            int assignedEmployeeId,
            string clientSiteName,
            string priority,
            CancellationToken cancellationToken = default);
    }
}
