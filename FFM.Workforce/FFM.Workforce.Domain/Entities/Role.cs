namespace FFM.Workforce.Domain.Entities;

public class Role
{
    public int Id { get; private set; }

    public string Name { get; private set; } = default!;

    public string Status { get; private set; } = default!;

    private Role() { }

    public Role(string name, string status)
    {
        Name = name;
        Status = status;
    }

    public Role(int id, string name, string status) : this(name, status)
    {
        Id = id;
    }
}
