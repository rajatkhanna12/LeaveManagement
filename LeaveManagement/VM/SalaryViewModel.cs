using LeaveManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.VM
{
    public class SalaryViewModel
    {
        private readonly LeaveDbContext _context;
        public SalaryViewModel(LeaveDbContext context)
        {
            _context = context;
        }

        public async Task<EstimatedSalaryViewModel> GetEstimatedSalaryDetailsAsync(string userId, int year, int month)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            decimal baseSalary = user.BaseSalary;
            var unpaidLeaveType = await _context.LeaveTypes.FirstOrDefaultAsync(x => x.Name.ToLower() == "unpaid");

            var unpaidLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == userId &&
                            l.LeaveTypeId == unpaidLeaveType.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.StartDate.Month == month &&
                l.StartDate.Year == year)
                .ToListAsync();

            var holidays = await _context.Holidays
                .Where(h => h.Date.Month == month && h.Date.Year == year)
                .Select(h => h.Date.Date)
                .ToListAsync();

            int unpaidLeaveDays = 0;
            foreach (var leave in unpaidLeaves)
            {
                var days = Enumerable.Range(0, (leave.EndDate - leave.StartDate).Days + 1)
                                     .Select(offset => leave.StartDate.AddDays(offset).Date)
                                     .Where(day => !holidays.Contains(day))
                                     .Count();

                unpaidLeaveDays += leave.IsHalfDay ? (int)(days * 0.5) : days;
            }

            int daysInMonth = DateTime.DaysInMonth(year, month);
            decimal dailyRate = baseSalary / daysInMonth;
            decimal deduction = unpaidLeaveDays * dailyRate;
            decimal estimated = baseSalary - deduction;

            return new EstimatedSalaryViewModel
            {
                FullName = user.FullName,
                Role = user.Role,
                JoiningDate = user.JoiningDate,
                BaseSalary = baseSalary,
                Month = month,
                Year = year,
                UnpaidLeaveDays = unpaidLeaveDays,
                Holidays = holidays,
                Deduction = deduction,
                EstimatedSalary = Math.Max(0, estimated)
            };
        }

    }
    public class EstimatedSalaryViewModel
    {
        public string FullName { get; set; }
        public string Role { get; set; }
        public DateTime JoiningDate { get; set; }
        public decimal BaseSalary { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }
        public int UnpaidLeaveDays { get; set; }
        public List<DateTime> Holidays { get; set; } = new();
        public decimal Deduction { get; set; }
        public decimal EstimatedSalary { get; set; }
    }

   
}
