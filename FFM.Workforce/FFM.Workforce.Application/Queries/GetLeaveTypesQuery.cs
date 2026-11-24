using System.Collections.Generic;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    public class GetLeaveTypesQuery : IRequest<List<LeaveTypeDto>>
    {
    }
}
