using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FFM.FieldOperations.Application.DTOs
{
    public class SubmitCustomerSignatureRequest
    {
        public int? VisitId { get; set; }
        public int? TaskId { get; set; }

        [Required(ErrorMessage = "CustomerName is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "CustomerName must be between 2 and 100 characters.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "SignatureData is required.")]
        public string SignatureData { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "CustomerRating must be an integer between 1 and 5.")]
        public int? CustomerRating { get; set; }

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        public string? Remarks { get; set; }
    }

    public class TaskEvidenceItemDto
    {
        public int EvidenceTypeId { get; set; } = 1;
        public string EvidenceType { get; set; } = "PHOTO";
        public string? FileName { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public Dictionary<string, object>? Readings { get; set; }
    }

    public class CompleteTaskWithEvidenceRequest
    {
        [Required(ErrorMessage = "TaskId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "TaskId must be greater than zero.")]
        public int TaskId { get; set; }

        public int? VisitId { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [Required(ErrorMessage = "CompletionNotes are required.")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "CompletionNotes must be between 5 and 1000 characters.")]
        public string CompletionNotes { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "CustomerName cannot exceed 100 characters.")]
        public string? CustomerName { get; set; }

        public string? CustomerSignatureUrl { get; set; }

        [Range(1, 5, ErrorMessage = "CustomerRating must be an integer between 1 and 5.")]
        public int? CustomerRating { get; set; }

        public List<TaskEvidenceItemDto>? Evidences { get; set; }
    }
}
