namespace FFM.Workforce.Application.DTOs
{
    public class LeaveTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal DefaultDaysPerYear { get; set; }
        public bool IsPaid { get; set; }
        public bool AllowNegativeBalance { get; set; }
    }
}
