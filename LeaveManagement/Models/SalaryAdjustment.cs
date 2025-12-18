using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public class SalaryAdjustment
    {
        [Key]
        public Guid AdjustmentID { get; set; }

        // Foreign Key
        public string EmployeeID { get; set; }
        public ApplicationUser Employee { get; set; }

        // Month & Year for salary cycle
        public int Month { get; set; }
        public int Year { get; set; }

        // Stored values
        public decimal LeavesTaken { get; set; }
        public decimal FreeLeavesUsed { get; set; }
        public decimal PaidLeavesDeducted { get; set; }
        public decimal FreeLeavesRemaining { get; set; }

        public decimal OneDaySalaryValue { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal FinalSalary { get; set; }

        // Approval
        public bool IsApproved { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? ApprovedBy { get; set; }
    }
}
