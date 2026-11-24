using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public record CorrectAttendanceCommand(
        int AttendanceId,
        DateTime? CheckInTime,
        DateTime? CheckOutTime,
        string Reason,
        int ActorUserId
    ) : IRequest<CorrectAttendanceResponseDto>;
}
