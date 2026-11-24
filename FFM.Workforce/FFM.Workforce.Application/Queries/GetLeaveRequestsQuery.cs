using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    public class GetLeaveRequestsQuery : IRequest<PagedResult<LeaveRequestDto>>
    {
        public int ActorUserId { get; set; }
        public int? EmployeeId { get; set; }
        public LeaveStatus? Status { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
