using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class EvidenceSchemaItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string EvidenceType { get; set; } = "PHOTO"; // PHOTO, SIGNATURE, METER_READING, DOCUMENT, CHECKLIST
        public string Widget { get; set; } = "CAMERA_CAPTURE"; // CAMERA_CAPTURE, SIGNATURE_PAD, NUMBER_INPUT, TEXT_INPUT, SWITCH, DROPDOWN
        public bool Required { get; set; } = true;
        public string? Unit { get; set; } // e.g. "PSI", "°C", "kW", "kWh"
        public string? Placeholder { get; set; }
        public string? Description { get; set; }
        public List<string>? Options { get; set; }
    }

    public class TaskEvidenceSchemaDto
    {
        public string TaskType { get; set; } = "GENERAL";
        public string Title { get; set; } = "Required Visit Evidence";
        public string Description { get; set; } = "Checklist of mandatory and optional proof-of-work items required before checkout.";
        public List<EvidenceSchemaItemDto> Items { get; set; } = new List<EvidenceSchemaItemDto>();
    }
}

