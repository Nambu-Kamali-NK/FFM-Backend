using System.Collections.Generic;

namespace FFM.Workforce.Application.DTOs
{
    public class BatchEmployeeStatusRequest
    {
        public List<int> EmployeeIds { get; set; } = new();
    }
}
