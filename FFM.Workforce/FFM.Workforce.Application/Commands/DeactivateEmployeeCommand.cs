using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class DeactivateEmployeeCommand : IRequest<bool>
    {
        public int EmployeeId { get; set; }
        public int ActorUserId { get; set; }
    }
}
