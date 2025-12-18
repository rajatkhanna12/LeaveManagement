using LeaveManagement.Models;

namespace LeaveManagement.VM
{
    public class EmployeeSalarySummaryVM
    {
        public ApplicationUser Employee { get; set; } = new();

        public SalaryAdjustment Adjustment { get; set; } = new();  

        public List<LeaveAdjustmentHistory> History { get; set; } = new();

        public string MonthName { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }
    }
}
