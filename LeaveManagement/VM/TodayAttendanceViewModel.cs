namespace LeaveManagement.VM
{
    public class TodayAttendanceViewModel
    {
        public Guid? UserId { get; set; }
        public string UserName { get; set; }

        // List of all attendance records for that user
        public List<AttendanceReportViewModel> AttendanceHistory { get; set; }
    
    }
}
