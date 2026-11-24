using System.ComponentModel.DataAnnotations;

namespace FFM.Workforce.Application.DTOs
{
    public class CancelLeaveRequest
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
    }
}
