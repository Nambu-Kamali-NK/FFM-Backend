using System;
using System.ComponentModel.DataAnnotations;

namespace FFM.Workforce.Application.DTOs
{
    public class SubmitPermissionRequest
    {
        [Required]
        public DateTime PermissionDate { get; set; }

        [Required]
        public string StartTime { get; set; } = string.Empty;

        [Required]
        public string EndTime { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class PermissionRequestDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime PermissionDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public decimal DurationHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ApproverId { get; set; }
        public string? DecisionComments { get; set; }
        public DateTime? DecidedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
