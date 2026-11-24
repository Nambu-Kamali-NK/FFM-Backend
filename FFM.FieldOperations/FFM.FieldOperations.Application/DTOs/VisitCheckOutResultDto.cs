using System;

namespace FFM.FieldOperations.Application.DTOs
{
    public class VisitCheckOutResultDto
    {
        public int VisitId { get; set; }
        public int TaskId { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public DateTime CheckOutTime { get; set; }
        public decimal DurationMinutes { get; set; }
        public string TaskStatus { get; set; } = string.Empty;
        public string CompletionNotes { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? CustomerSignatureUrl { get; set; }
        public int? CustomerRating { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

