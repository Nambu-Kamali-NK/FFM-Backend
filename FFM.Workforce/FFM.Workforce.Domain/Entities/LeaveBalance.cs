using System;

namespace FFM.Workforce.Domain.Entities
{
    public class LeaveBalance
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public int LeaveTypeId { get; private set; }
        public LeaveType LeaveType { get; private set; } = default!;
        public int Year { get; private set; }
        public decimal TotalDays { get; private set; }
        public decimal UsedDays { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public decimal AvailableDays => TotalDays - UsedDays;

        private LeaveBalance() { }

        public LeaveBalance(int employeeId, int leaveTypeId, int year, decimal totalDays, decimal usedDays = 0)
        {
            EmployeeId = employeeId;
            LeaveTypeId = leaveTypeId;
            Year = year;
            TotalDays = totalDays;
            UsedDays = usedDays;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool CanDeduct(decimal days)
        {
            if (LeaveType != null && LeaveType.AllowNegativeBalance)
                return true;

            return AvailableDays >= days;
        }

        public void DeductDays(decimal days)
        {
            UsedDays += days;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RefundDays(decimal days)
        {
            UsedDays = Math.Max(0, UsedDays - days);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
