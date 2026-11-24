using MediatR;
using FFM.Workforce.Application.DTOs;

namespace FFM.Workforce.Application.Commands
{
    /// <summary>
    /// Command to edit an existing employee's editable profile and workforce information.
    /// Immutable fields (Id, UserId, EmployeeCode, CreatedAt, CreatedBy, OrgUnitId, Status)
    /// cannot be modified by the client.
    /// </summary>
    public class EditEmployeeCommand : IRequest<EditEmployeeResponseDto>
    {
        /// <summary>
        /// Numeric Employee ID from the route.
        /// </summary>
        public int Id { get; set; }

        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public int RoleId { get; set; }
        public int? ManagerId { get; set; }

        /// <summary>
        /// Resolved strictly from the authenticated JWT claims.
        /// Never trusted from client request body.
        /// </summary>
        public int ActorUserId { get; set; }
    }
}
