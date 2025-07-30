namespace LeaveManagement.Models
{
    public class LeaveSummaryModel
    {

     
            public string LeaveTypeName { get; set; }
            public int TotalAllocated { get; set; }
            public int Used { get; set; }
            public int Remaining { get; set; }
        
    }

    public class SalaryModel
    {
        public decimal BaseSalary { get; set; }
        public double TotalLeaves { get; set; }
        public decimal EstimatedSalary { get; set; }
        public decimal PerDaySalary { get; set; }
        public int TotalDaysInMonth { get; set; }
    }
}
