using System;
using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Queries
{
    /// <summary>
    /// Query to retrieve paginated employees with sorting options.
    /// </summary>
    public class GetAllEmployeesQuery : IRequest<PagedResult<EmployeeDto>>
    {
        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Sorting
        public string? SortBy { get; set; } = "EmployeeCode";
        public string? SortOrder { get; set; } = "asc"; // "asc" or "desc"
    }
}
