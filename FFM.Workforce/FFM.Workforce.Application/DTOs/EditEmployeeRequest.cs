namespace FFM.Workforce.Application.DTOs
{
    /// <summary>
    /// Request payload for editing an employee.
    /// Includes target Employee ID in request body. Excludes server-controlled fields such as ActorUserId, OrgUnitId, EmployeeCode, and Status.
    /// </summary>
    public class EditEmployeeRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public int RoleId { get; set; }
        public int? ManagerId { get; set; }
    }
}
