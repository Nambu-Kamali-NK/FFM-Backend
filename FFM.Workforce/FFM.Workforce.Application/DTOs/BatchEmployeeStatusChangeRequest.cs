using System.Collections.Generic;

namespace FFM.Workforce.Application.DTOs
{
    public class BatchEmployeeStatusChangeRequest
    {
        public List<int> EmployeeIds { get; set; } = new();
        public bool IsActive { get; set; }
    }
}
