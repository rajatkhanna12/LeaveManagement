namespace LeaveManagement.Models
{
    public class TblAttendance
    {
        public int Id { get; set; }

        public DateTime? CheckedInTime { get; set; }

        public DateTime? CheckedoutTime { get; set; }

        public string? CheckedinImage { get; set; }

        public string? CheckedoutImage { get; set; }

        public DateTime CreatedDate { get; set; }

        public Guid UserId { get; set; }
    }
}
