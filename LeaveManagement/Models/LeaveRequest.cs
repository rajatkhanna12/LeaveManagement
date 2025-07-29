namespace LeaveManagement.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsHalfDay { get; set; }

        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; }

        public string Reason { get; set; }
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
        public DateTime AppliedOn { get; set; } = DateTime.UtcNow;
    }

    public enum LeaveStatus
    {
        Pending, Approved, Rejected
    }

}
