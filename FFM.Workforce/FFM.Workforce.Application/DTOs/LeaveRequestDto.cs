using System;
using System.ComponentModel.DataAnnotations;

namespace FFM.Workforce.Application.DTOs
{
    public class SubmitLeaveRequest
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "A valid LeaveTypeId is required.")]
        public int LeaveTypeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsHalfDay { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class LeaveRequestDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int LeaveTypeId { get; set; }
        public string LeaveTypeName { get; set; } = string.Empty;
        public string LeaveTypeCode { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal DaysCount { get; set; }
        public bool IsHalfDay { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ApproverId { get; set; }
        public string? DecisionComments { get; set; }
        public DateTime? DecidedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
