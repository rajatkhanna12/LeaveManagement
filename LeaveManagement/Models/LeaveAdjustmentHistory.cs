using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public class LeaveAdjustmentHistory
    {
        [Key]
        public Guid HistoryID { get; set; }

        // FK
        public string EmployeeID { get; set; }
        public ApplicationUser Employee { get; set; }

        public DateTime Date { get; set; }              // Adjustment date
        public string LeaveType { get; set; }           // Full / Half
        public string Action { get; set; }              // Free Leave Used / Salary Deducted
        public decimal FreeLeavesLeft { get; set; }     // After adjustment
        public decimal PaidLeaves { get; set; }         // Paid leave deducted
        public int Month { get; set; }
        public int Year { get; set; }
    }
}
