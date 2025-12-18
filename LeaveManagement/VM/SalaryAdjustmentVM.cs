using LeaveManagement.Models;

namespace LeaveManagement.VM
{
    public class SalaryAdjustmentVM
    {
        public ApplicationUser Employee { get; set; }
        public List<LeaveRequest> PreviousMonthLeaves { get; set; }
        public DateTime PreviousMonthStart { get; set; }
        public DateTime PreviousMonthEnd { get; set; }
        public int TotalWorkingDays { get; set; }   
        public double LeavesTaken { get; set; }
        public double FreeLeavesAvailable { get; set; }
        public List<LeaveAdjustmentHistory> History { get; set; }

    }
}
