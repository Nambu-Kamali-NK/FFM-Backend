using System;

namespace FFM.FieldOperations.Domain.Models
{
    public class TaskFilterParameters
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
