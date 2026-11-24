namespace FFM.Workforce.Application.DTOs
{
    public class LeaveBalanceDto
    {
        public int LeaveTypeId { get; set; }
        public string LeaveTypeCode { get; set; } = default!;
        public string LeaveTypeName { get; set; } = default!;
        public int Year { get; set; }
        public decimal TotalDays { get; set; }
        public decimal UsedDays { get; set; }
        public decimal AvailableDays { get; set; }
    }
}
