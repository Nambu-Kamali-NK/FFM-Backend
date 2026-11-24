using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class SubmitLeaveRequestCommand : IRequest<LeaveRequestDto>
    {
        public int ActorUserId { get; set; }
        public int LeaveTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsHalfDay { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
