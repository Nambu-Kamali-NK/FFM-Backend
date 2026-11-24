using System.Collections.Generic;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    public class GetLeaveBalancesQuery : IRequest<List<LeaveBalanceDto>>
    {
        public int ActorUserId { get; set; }
        public int? TargetEmployeeId { get; set; }
        public int? Year { get; set; }
    }
}
