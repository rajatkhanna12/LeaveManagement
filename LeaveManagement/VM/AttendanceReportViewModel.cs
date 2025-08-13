namespace LeaveManagement.VM
{
    public class AttendanceReportViewModel
    {
        public DateTime CheckedInTime { get; set; }
        public DateTime? CheckedOutTime { get; set; }
        public string CheckedInImage { get; set; }
        public string CheckedOutImage { get; set; }
        public double? WorkingHours { get; set; }
    }
}
