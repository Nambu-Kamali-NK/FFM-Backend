namespace FFM.Workforce.Application.DTOs
{
    /// <summary>
    /// Request payload for creating an employee.
    /// Excludes server-controlled fields such as ActorUserId, OrgUnitId, and Status.
    /// </summary>
    public class CreateEmployeeRequest
    {
        public string EmployeeCode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Password { get; set; } = default!;
        public int RoleId { get; set; }
        public int? ManagerId { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
    }
}
