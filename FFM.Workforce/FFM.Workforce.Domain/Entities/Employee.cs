namespace FFM.Workforce.Domain.Entities
{
    public enum EmployeeStatus
    {
        PENDING_ACTIVATION,
        ACTIVE,
        DEACTIVATED
    }

    public class Employee
    {
        public int Id { get; private set; }
        public string EmployeeCode { get; private set; } = default!;
        /// <summary>
        /// Link to the Identity user account. Required — every employee must have a login account.
        /// </summary>
        public int UserId { get; private set; }
        public string Name { get; private set; } = default!;
        public string? Email { get; private set; }
        public string? Mobile { get; private set; }

        public int RoleId { get; private set; }
        public Role Role { get; private set; } = default!;

        public Guid OrgUnitId { get; private set; }
        public int? ManagerId { get; private set; }
        public EmployeeStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string CreatedBy { get; private set; } = default!;

        private Employee() { }

        public Employee(
            string employeeCode,
            int userId,
            string name,
            int roleId,
            Guid orgUnitId,
            int? managerId,
            string? email,
            string? mobile,
            string createdBy)
        {
            EmployeeCode = employeeCode;
            UserId = userId;
            Name = name;
            RoleId = roleId;
            OrgUnitId = orgUnitId;
            ManagerId = managerId;
            Email = email;
            Mobile = mobile;
            Status = EmployeeStatus.PENDING_ACTIVATION;
            CreatedAt = DateTime.UtcNow;
            CreatedBy = createdBy;
        }

        public Employee(
            int id,
            string employeeCode,
            int userId,
            string name,
            int roleId,
            Guid orgUnitId,
            int? managerId,
            string? email,
            string? mobile,
            string createdBy,
            EmployeeStatus status = EmployeeStatus.PENDING_ACTIVATION)
            : this(employeeCode, userId, name, roleId, orgUnitId, managerId, email, mobile, createdBy)
        {
            Id = id;
            Status = status;
        }

        public void Update(
            string name,
            string? email,
            string? mobile,
            int roleId,
            int? managerId)
        {
            Name = name;
            Email = email;
            Mobile = mobile;
            RoleId = roleId;
            ManagerId = managerId;
        }

        public void Activate()
        {
            if (Status == EmployeeStatus.ACTIVE)
                throw new InvalidOperationException("Employee is already active.");

            Status = EmployeeStatus.ACTIVE;
        }

        public void Deactivate()
        {
            if (Status == EmployeeStatus.DEACTIVATED)
                throw new InvalidOperationException("Employee is already deactivated.");

            Status = EmployeeStatus.DEACTIVATED;
        }
    }
}

