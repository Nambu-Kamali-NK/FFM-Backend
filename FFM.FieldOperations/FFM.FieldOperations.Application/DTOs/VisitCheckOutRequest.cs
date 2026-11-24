using System.ComponentModel.DataAnnotations;

namespace FFM.FieldOperations.Application.DTOs
{
    public class VisitCheckOutRequest
    {
        public int? VisitId { get; set; }
        public int? TaskId { get; set; }

        [Required(ErrorMessage = "Latitude is required.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90.0 and 90.0.")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180.0 and 180.0.")]
        public decimal Longitude { get; set; }

        [Required(ErrorMessage = "CompletionNotes is required.")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "CompletionNotes must be between 5 and 1000 characters.")]
        public string CompletionNotes { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "CustomerName cannot exceed 100 characters.")]
        public string? CustomerName { get; set; }

        [StringLength(500, ErrorMessage = "CustomerSignatureUrl cannot exceed 500 characters.")]
        public string? CustomerSignatureUrl { get; set; }

        [Range(1, 5, ErrorMessage = "CustomerRating must be an integer between 1 and 5.")]
        public int? CustomerRating { get; set; }
    }
}

