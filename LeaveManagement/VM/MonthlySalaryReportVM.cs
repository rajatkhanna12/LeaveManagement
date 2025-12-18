using LeaveManagement.Models;

namespace LeaveManagement.VM
{
    public class MonthlySalaryReportVM
    {
        public ApplicationUser Employee { get; set; } 
        public decimal? FinalSalary { get; set; }  
    }
}
