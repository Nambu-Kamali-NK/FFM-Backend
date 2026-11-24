using System;

namespace FFM.Workforce.Domain.Entities
{
    public class LeaveType
    {
        public int Id { get; private set; }
        public string Code { get; private set; } = default!;
        public string Name { get; private set; } = default!;
        public decimal DefaultDaysPerYear { get; private set; }
        public bool IsPaid { get; private set; }
        public bool AllowNegativeBalance { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private LeaveType() { }

        public LeaveType(
            int id,
            string code,
            string name,
            decimal defaultDaysPerYear,
            bool isPaid,
            bool allowNegativeBalance,
            bool isActive = true,
            DateTime? createdAt = null)
        {
            Id = id;
            Code = code;
            Name = name;
            DefaultDaysPerYear = defaultDaysPerYear;
            IsPaid = isPaid;
            AllowNegativeBalance = allowNegativeBalance;
            IsActive = isActive;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
