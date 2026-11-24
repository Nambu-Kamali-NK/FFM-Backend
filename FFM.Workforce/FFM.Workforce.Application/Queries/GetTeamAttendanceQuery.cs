using System;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    public class GetTeamAttendanceQuery : IRequest<PagedResult<TeamAttendanceDto>>
    {
        public int ActorUserId { get; set; }
        public Guid? OrgUnitId { get; set; }
        public DateTime? Date { get; set; }
        public AttendanceStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
