namespace FFM.Workforce.Domain.Entities;

/// <summary>
/// Represents an organisational unit (department, team, division, etc.).
/// Employees are assigned to an OrgUnit. One OrgUnit is designated as the
/// default (IsDefault = true) and is automatically assigned to new employees
/// when no explicit unit is specified.
/// </summary>
public class OrgUnit
{
    public Guid Id { get; private set; }

    /// <summary>Human-readable name, e.g. "Default Organization".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Classification, e.g. "ORGANIZATION", "DEPARTMENT", "TEAM".</summary>
    public string Type { get; private set; } = default!;

    /// <summary>Optional parent unit Id for hierarchy.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>"ACTIVE" or "INACTIVE".</summary>
    public string Status { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    /// <summary>
    /// Exactly one OrgUnit in the system should have IsDefault = true and Status = "ACTIVE".
    /// The handler uses this flag to auto-assign new employees.
    /// </summary>
    public bool IsDefault { get; private set; }

    // Required by EF Core
    private OrgUnit() { }

    public OrgUnit(
        string name,
        string type,
        string status,
        Guid createdBy,
        bool isDefault = false,
        Guid? parentId = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        Status = status;
        CreatedBy = createdBy;
        IsDefault = isDefault;
        ParentId = parentId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Constructor used when a specific stable Id is needed (e.g. data seeding).
    /// </summary>
    public OrgUnit(
        Guid id,
        string name,
        string type,
        string status,
        Guid createdBy,
        bool isDefault = false,
        Guid? parentId = null)
    {
        Id = id;
        Name = name;
        Type = type;
        Status = status;
        CreatedBy = createdBy;
        IsDefault = isDefault;
        ParentId = parentId;
        CreatedAt = DateTime.UtcNow;
    }
}
