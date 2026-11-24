using System;

namespace FFM.Workforce.Application.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public int? UserId { get; set; }
    public string Name { get; set; } = default!;
    public int RoleId { get; set; }
    public Guid OrgUnitId { get; set; }
    public int? ManagerId { get; set; }
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = default!;
}