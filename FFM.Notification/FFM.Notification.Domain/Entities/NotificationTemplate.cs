namespace FFM.Notification.Domain.Entities;

public class NotificationTemplate
{
    public int Id { get; private set; }
    public string TemplateCode { get; private set; } = default!;
    public string Channel { get; private set; } = default!; // PUSH, IN_APP, SMS, EMAIL
    public string? Title { get; private set; }
    public string BodyTemplate { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected NotificationTemplate() { }

    public NotificationTemplate(
        string templateCode,
        string channel,
        string? title,
        string bodyTemplate,
        string? description,
        int createdByUserId)
    {
        TemplateCode = templateCode.Trim().ToUpperInvariant();
        Channel = channel.Trim().ToUpperInvariant();
        Title = title?.Trim();
        BodyTemplate = bodyTemplate;
        Description = description?.Trim();
        IsActive = true;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string? title, string bodyTemplate, string? description, bool isActive, int updatedByUserId)
    {
        Title = title?.Trim();
        BodyTemplate = bodyTemplate;
        Description = description?.Trim();
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

