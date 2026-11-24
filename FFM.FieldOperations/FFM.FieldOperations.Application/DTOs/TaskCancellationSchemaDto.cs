using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class CancellationReasonOptionDto
    {
        public string ReasonCode { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiresFollowUpNote { get; set; } = false;
        public string? FollowUpPlaceholder { get; set; }
        public bool RequiresPhotoProof { get; set; } = false;
    }

    public class TaskCancellationSchemaDto
    {
        public string Title { get; set; } = "Cancel Work Order";
        public string Description { get; set; } = "Select the reason for cancelling this task. Accurate cancellation telemetry is required for SLA audits.";
        public List<CancellationReasonOptionDto> Reasons { get; set; } = new List<CancellationReasonOptionDto>();
    }
}

