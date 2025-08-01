namespace LeaveManagement.Models
{
    public class UserLeaveSummaryViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string LeaveTypeName { get; set; }
        public int TotalAllocated { get; set; }
        public double Used { get; set; }
        public double Remaining { get; set; }
    }
}
