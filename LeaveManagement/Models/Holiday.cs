using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public class Holiday
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }
        public bool IsRecurringAnnually { get; set; } = true;
        public string? Description { get; set; }
    }

}
