namespace LeaveManagement.VM.DTOs
{
    public class SaveSalaryDto
    {
        public string EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal LeavesTaken { get; set; }
        public decimal FreeLeavesUsed { get; set; }
        public decimal PaidLeavesDeducted { get; set; }
        public decimal FreeLeavesRemaining { get; set; }
        public decimal OneDaySalaryValue { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal FinalSalary { get; set; }
    }
}
