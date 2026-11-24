using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    public class GetMyAttendanceHistoryQuery : IRequest<PagedResult<AttendanceHistoryDto>>
    {
        public int ActorUserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
