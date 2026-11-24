using System;

namespace FFM.FieldOperations.Domain.Entities
{
    public class EvidenceType
    {
        public int Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string WidgetType { get; private set; } = "IMAGE_PICKER";
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        private EvidenceType() { }

        public EvidenceType(int id, string code, string name, string? description = null, string widgetType = "IMAGE_PICKER", bool isActive = true)
        {
            if (id <= 0)
                throw new ArgumentException("Id must be greater than zero.", nameof(id));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code cannot be empty.", nameof(code));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));

            Id = id;
            Code = code.Trim().ToUpperInvariant();
            Name = name.Trim();
            Description = description?.Trim();
            WidgetType = string.IsNullOrWhiteSpace(widgetType) ? "IMAGE_PICKER" : widgetType.Trim().ToUpperInvariant();
            IsActive = isActive;
            CreatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }
    }
}

