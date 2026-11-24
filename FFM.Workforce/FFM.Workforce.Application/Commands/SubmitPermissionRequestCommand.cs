using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class SubmitPermissionRequestCommand : IRequest<PermissionRequestDto>
    {
        public int ActorUserId { get; set; }
        public DateTime PermissionDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
