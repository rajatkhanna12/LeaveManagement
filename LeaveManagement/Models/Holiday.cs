using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public class Holiday
    {
        public int Id { get; set; }       
        public DateTime HolidayDate { get; set; }
        public string HolidayName { get; set; }
        public string HolidayType { get; set; } // National / Optional
        public string Description { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public int? CreatedBy { get; set; }

    }

}
