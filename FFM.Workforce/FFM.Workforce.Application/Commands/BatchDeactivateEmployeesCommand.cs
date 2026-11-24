using System.Collections.Generic;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class BatchDeactivateEmployeesCommand : IRequest<int>
    {
        public List<int> EmployeeIds { get; set; } = new();
        public int ActorUserId { get; set; }
    }
}
