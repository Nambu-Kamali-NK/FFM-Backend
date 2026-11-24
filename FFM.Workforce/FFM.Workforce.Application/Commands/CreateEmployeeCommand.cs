using System;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    /// <summary>
    /// DTO used to request creation of a new employee.
    /// The client supplies only business data. Server-side fields (UserId, OrgUnitId,
    /// EmployeeId, CreatedBy, CreatedAt) are set automatically by the backend.
    /// </summary>
    public class CreateEmployeeCommand : IRequest<int>
    {
        // Client-supplied mandatory fields
        public string EmployeeCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Password { get; set; } = default!;
        public int RoleId { get; set; }
        public int? ManagerId { get; set; }

        // Optional contact fields — validated when supplied.
        public string? Email { get; set; }
        public string? Mobile { get; set; }

        // Server-populated from JWT — never supplied by the client unless explicitly set.
        public int ActorUserId { get; set; }
        public string? CreatedBy { get; set; }
    }
}
