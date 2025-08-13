using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Models
{
    public class LeaveDbContext : IdentityDbContext<ApplicationUser>
    {
        public LeaveDbContext(DbContextOptions<LeaveDbContext> options) : base(options) { }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<SalaryReport> SalaryReports { get; set; }
        public DbSet<TblAttendance> tblAttendances { get; set; }
       
    }
}
