using System;
using System.ComponentModel.DataAnnotations;

namespace FFM.FieldOperations.Application.DTOs
{
    public class CreateTaskRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        public string? Description { get; set; }

        public string? Priority { get; set; } = "MEDIUM";

        [Required(ErrorMessage = "ClientSiteId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Valid ClientSiteId is required.")]
        public int ClientSiteId { get; set; }

        public int? AssignedEmployeeId { get; set; }

        [Required(ErrorMessage = "ScheduledStart is required.")]
        public DateTime ScheduledStart { get; set; }

        [Required(ErrorMessage = "ScheduledEnd is required.")]
        public DateTime ScheduledEnd { get; set; }

        public string? TaskType { get; set; } = "GENERAL";

        public System.Collections.Generic.Dictionary<string, object>? CustomData { get; set; }
    }

    public class TaskDto
    {
        public int Id { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string TaskType { get; set; } = "GENERAL";
        public System.Collections.Generic.Dictionary<string, object>? CustomData { get; set; }
        public int ClientSiteId { get; set; }
        public string ClientSiteName { get; set; } = string.Empty;
        public string ClientSiteCode { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public int CreatedByUserId { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AssignTaskRequest
    {
        [Required(ErrorMessage = "TaskId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Valid TaskId is required.")]
        public int TaskId { get; set; }

        [Required(ErrorMessage = "AssignedEmployeeId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Valid AssignedEmployeeId is required.")]
        public int AssignedEmployeeId { get; set; }
    }

    public class CancelTaskRequest
    {
        [Required(ErrorMessage = "TaskId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Valid TaskId is required.")]
        public int TaskId { get; set; }

        [Required(ErrorMessage = "Cancellation reason is required.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Cancellation reason must be between 5 and 500 characters.")]
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdateTaskStatusRequest
    {
        [Required(ErrorMessage = "TaskId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Valid TaskId is required.")]
        public int TaskId { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        public string Status { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    public class GetTasksFilterQuery
    {
        public int? EmployeeId { get; set; }
        public string? Status { get; set; }
        public int? ClientSiteId { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "scheduledStart";
        public string? SortOrder { get; set; } = "asc";
    }
}
