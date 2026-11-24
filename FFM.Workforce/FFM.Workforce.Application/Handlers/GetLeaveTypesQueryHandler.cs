using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetLeaveTypesQueryHandler : IRequestHandler<GetLeaveTypesQuery, List<LeaveTypeDto>>
    {
        private readonly ILeaveRepository _leaveRepository;

        public GetLeaveTypesQueryHandler(ILeaveRepository leaveRepository)
        {
            _leaveRepository = leaveRepository;
        }

        public async Task<List<LeaveTypeDto>> Handle(GetLeaveTypesQuery request, CancellationToken cancellationToken)
        {
            var types = await _leaveRepository.GetLeaveTypesAsync(activeOnly: true);

            return types.Select(t => new LeaveTypeDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                DefaultDaysPerYear = t.DefaultDaysPerYear,
                IsPaid = t.IsPaid,
                AllowNegativeBalance = t.AllowNegativeBalance
            }).ToList();
        }
    }
}
