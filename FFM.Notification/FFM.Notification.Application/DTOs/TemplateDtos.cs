namespace FFM.Notification.Application.DTOs;

public class CreateNotificationTemplateRequest
{
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!; // PUSH, IN_APP, SMS, EMAIL
    public string? Title { get; set; }
    public string BodyTemplate { get; set; } = default!;
    public string? Description { get; set; }
}

public class UpdateNotificationTemplateRequest
{
    public int TemplateId { get; set; }
    public string? Title { get; set; }
    public string BodyTemplate { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RenderTemplateRequest
{
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public Dictionary<string, string> Placeholders { get; set; } = new();
}

public class NotificationTemplateResponse
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string? Title { get; set; }
    public string BodyTemplate { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RenderedTemplateResponse
{
    public string TemplateCode { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string? RenderedTitle { get; set; }
    public string RenderedBody { get; set; } = default!;
}

