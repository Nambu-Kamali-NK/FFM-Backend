using FFM.Workforce.Application.DTOs;
using MediatR;
using System;

namespace FFM.Workforce.Application.Queries;

public class GetEmployeeByIdQuery : IRequest<EmployeeDto?>
{
    public int EmployeeId { get; }

    public GetEmployeeByIdQuery(int employeeId)
    {
        EmployeeId = employeeId;
    }
}
