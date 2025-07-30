using LeaveManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.VM
{
    public class SalaryService
    {
        private readonly LeaveDbContext _context;
        public SalaryService(LeaveDbContext context)
        {
            _context = context;
        }

        //public async Task<EstimatedSalaryViewModel> GetEstimatedSalaryDetailsAsync(string userId, int year, int month)
        //{
            
        //}

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
