using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Security.Claims;
using LeaveManagement.VM.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Controllers
{
    [Authorize(Roles = "Manager")]
    public class AdminController : Controller
    {
        private readonly LeaveDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        public AdminController(LeaveDbContext context, UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }
        // Logged In user
        private async Task SetUserInfoAsync()
        {
            if (User?.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    ViewBag.UserName = !string.IsNullOrEmpty(user.FullName) ? user.FullName : user.UserName;

                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.UserRole = roles?.FirstOrDefault() ?? "Employee";
                    return;
                }
            }

            // Default values (for safety)
            ViewBag.UserName = "Guest";
            ViewBag.UserRole = "Unknown";
        }

        public async Task<IActionResult> Index()
        {
            await SetUserInfoAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            var today = DateTime.Today;

            // 🎂 Employees having Birthday today
            var birthdayEmployees = employees
                .Where(e => e.DateOfBirth.Day == today.Day &&
                            e.DateOfBirth.Month == today.Month)
                .ToList();

            // 🎊 Employees having Work Anniversary today
            var anniversaryEmployees = employees
              .Where(e => e.JoiningDate.Month == today.Month &&
                          e.JoiningDate.Day == today.Day)
              .Select(e => new
              {
                  e.FullName,
                  e.JoiningDate
              })
              .ToList();

            ViewBag.BirthdayEmployees = birthdayEmployees;
            ViewBag.AnniversaryEmployees = anniversaryEmployees;
            return View(employees.AsEnumerable());
        }
        public async Task<IActionResult> MonthlySalaryReport()
        {
            await SetUserInfoAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

            var today = DateTime.Today;
            int month = today.AddMonths(-1).Month;
            int year = today.AddMonths(-1).Year;

            var salaryAdjustments = await _context.SalaryAdjustments
                            .Where(x => x.Month == month && x.Year == year)
                            .ToListAsync();
            var viewModel = employees.Select(e => new MonthlySalaryReportVM
            {
                Employee = e,
                FinalSalary = salaryAdjustments
          .FirstOrDefault(x => x.EmployeeID == e.Id)?.FinalSalary
            });
            var prev = today.AddMonths(-1);
            ViewBag.Month = prev.ToString("MMMM yyyy"); ;
            return View(viewModel);
        }
        private async Task<double> GetAvailableFreeLeavesForEmployee(string empId)
        {
            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            int currentYear = today.Year;

            var startOfYear = new DateTime(currentYear, 1, 1);
            var endOfCurrentMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

            // Get approved leaves
            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == empId &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfYear &&
                            l.StartDate <= endOfCurrentMonth)
                .ToListAsync();

            // Flatten leave days
            var leaveDays = new List<(DateTime date, bool isHalfDay)>();

            foreach (var leave in approvedLeaves)
            {
                var leaveStart = leave.StartDate < startOfYear ? startOfYear : leave.StartDate;
                var leaveEnd = leave.EndDate > endOfCurrentMonth ? endOfCurrentMonth : leave.EndDate;

                for (var dt = leaveStart; dt <= leaveEnd; dt = dt.AddDays(1))
                {
                    if (dt.Year == currentYear && dt.Month <= currentMonth)
                    {
                        leaveDays.Add((dt, leave.IsHalfDay));
                    }
                }
            }

            // Group by month
            var leavesByMonth = leaveDays
                .GroupBy(ld => ld.date.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            double carryForward = 0;
            double totalPaidUsed = 0;

            for (int month = 1; month <= currentMonth; month++)
            {
                double allowed = 1; // 1 monthly free leave
                double takenThisMonth = 0;

                if (leavesByMonth.ContainsKey(month))
                {
                    takenThisMonth = leavesByMonth[month].Sum(ld => ld.isHalfDay ? 0.5 : 1);
                }

                double available = allowed + carryForward;
                double paidUsed = Math.Min(takenThisMonth, available);

                carryForward = Math.Max(0, available - paidUsed);
                totalPaidUsed += paidUsed;
            }

            return Math.Round(carryForward, 1); // Remaining free leaves
        }

        public async Task<IActionResult> SalaryAdjustment(string empId)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return NotFound();

            var employee = await _userManager.FindByIdAsync(empId);

            if (employee == null)
                return NotFound();
            var today = DateTime.Today;
            var prevMonthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            var prevMonthEnd = new DateTime(today.Year, today.Month, 1).AddDays(-1);
            var prevApprovedLeaves = await _context.LeaveRequests
                        .Where(lr =>
                            lr.UserId == empId &&
                            lr.Status == LeaveStatus.Approved &&
                            lr.StartDate >= prevMonthStart &&
                            lr.EndDate <= prevMonthEnd)
                        .ToListAsync();
            var leavesTaken = prevApprovedLeaves
                    .Sum(x => x.IsHalfDay ? 0.5 : (x.EndDate - x.StartDate).TotalDays + 1);
            double freeLeavesAvailable = Convert.ToDouble(employee.FreeLeavesLeft);

            ViewBag.PrevMonthName = prevMonthStart.ToString("MMMM yyyy");
            var history = await _context.LeaveAdjustmentHistories
                        .Where(h => h.EmployeeID == empId &&
                                    h.Month == prevMonthStart.Month &&
                                    h.Year == prevMonthStart.Year)
                        .OrderByDescending(h => h.Date)
                        .ToListAsync();


            var vm = new SalaryAdjustmentVM
            {
                Employee = employee,
                PreviousMonthLeaves = prevApprovedLeaves,
                PreviousMonthStart = prevMonthStart,
                PreviousMonthEnd = prevMonthEnd,
                TotalWorkingDays = (prevMonthEnd - prevMonthStart).Days + 1,
                LeavesTaken = leavesTaken,
                FreeLeavesAvailable  = freeLeavesAvailable,
                History = history,

            };
            return View(vm); 
        }
        [HttpPost]
        public async Task<IActionResult> SaveSalaryAdjustment([FromBody] SaveSalaryDto dto)
        {
            var admin = await _userManager.GetUserAsync(User);

            var employeeUser = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.EmployeeId);
            if (employeeUser == null)
                return Json(new { success = false, message = "Employee not found." });

            decimal currentFree = employeeUser.FreeLeavesLeft.GetValueOrDefault();

            var existing = await _context.SalaryAdjustments
                   .FirstOrDefaultAsync(x =>
                       x.EmployeeID == dto.EmployeeId &&
                       x.Month == dto.Month &&
                       x.Year == dto.Year);


            SalaryAdjustment adjustment;
            if(existing != null)
            {
                adjustment = existing;

                // ⭐ ROLLBACK OLD FREE LEAVE + APPLY NEW FREE LEAVE ⭐
                currentFree += existing.FreeLeavesUsed;  // rollback
                currentFree -= dto.FreeLeavesUsed;       // apply new

                currentFree = Math.Round(currentFree, 1);
                if (currentFree < 0) currentFree = 0;

                employeeUser.FreeLeavesLeft = currentFree;
                _context.Users.Update(employeeUser);


                adjustment.LeavesTaken = dto.LeavesTaken;
                adjustment.FreeLeavesUsed = dto.FreeLeavesUsed;
                adjustment.PaidLeavesDeducted = dto.PaidLeavesDeducted;
                adjustment.FreeLeavesRemaining = currentFree;

                adjustment.OneDaySalaryValue = dto.OneDaySalaryValue;
                adjustment.TotalDeduction = dto.TotalDeduction;
                adjustment.FinalSalary = dto.FinalSalary;

                adjustment.IsApproved = true;
                adjustment.ApprovedOn = DateTime.Now;
                adjustment.ApprovedBy = admin.Id;

                _context.SalaryAdjustments.Update(adjustment);
            }
            else
            {
                 adjustment = new SalaryAdjustment
                {
                    EmployeeID = dto.EmployeeId,
                    Month = dto.Month,
                    Year = dto.Year,

                    LeavesTaken = dto.LeavesTaken,
                    FreeLeavesUsed = dto.FreeLeavesUsed,
                    PaidLeavesDeducted = dto.PaidLeavesDeducted,
                    FreeLeavesRemaining = Math.Round(dto.FreeLeavesRemaining, 1),

                    OneDaySalaryValue = dto.OneDaySalaryValue,
                    TotalDeduction = dto.TotalDeduction,
                    FinalSalary = dto.FinalSalary,

                    IsApproved = true,
                    ApprovedOn = DateTime.Now,
                    ApprovedBy = admin.Id
                };

                currentFree -= dto.FreeLeavesUsed;
                currentFree = Math.Round(currentFree, 1);
                if (currentFree < 0) currentFree = 0;

                employeeUser.FreeLeavesLeft = currentFree;
                _context.Users.Update(employeeUser);

                adjustment.FreeLeavesRemaining = currentFree;


                _context.SalaryAdjustments.Add(adjustment);
            }
            var oldHistory = _context.LeaveAdjustmentHistories
                 .Where(x => x.EmployeeID == dto.EmployeeId &&
                             x.Month == dto.Month &&
                             x.Year == dto.Year);

            _context.LeaveAdjustmentHistories.RemoveRange(oldHistory);

            decimal freeUsed = dto.FreeLeavesUsed;
            int freeFull = (int)Math.Floor((double)freeUsed); 
            bool freeHalf = (freeUsed - freeFull) == 0.5m;

            decimal paidUsed = dto.PaidLeavesDeducted;
            int paidFull = (int)Math.Floor((double)paidUsed);
            bool paidHalf = (paidUsed - paidFull) == 0.5m;


            // add full day free  entry 
            if (freeFull != 0)
            {
                _context.LeaveAdjustmentHistories.Add(new LeaveAdjustmentHistory
                {
                    EmployeeID = dto.EmployeeId,
                    Date = DateTime.Now,
                    LeaveType = "Full Day",
                    Action = "Free Leave Used",
                    FreeLeavesLeft = currentFree,
                    PaidLeaves = 0,
                    Month = dto.Month,
                    Year = dto.Year
                });
            }
            //Add half-day free entry
            if (freeHalf)
            {
                _context.LeaveAdjustmentHistories.Add(new LeaveAdjustmentHistory
                {
                    EmployeeID = dto.EmployeeId,
                    Date = DateTime.Now,
                    LeaveType = "Half Day",
                    Action = "Free Leave Used",
                    FreeLeavesLeft = currentFree,
                    PaidLeaves = 0,
                    Month = dto.Month,
                    Year = dto.Year
                });
            }
            //Add full-day paid (deduction) entries
            if (paidFull != 0)
            {
                _context.LeaveAdjustmentHistories.Add(new LeaveAdjustmentHistory
                {
                    EmployeeID = dto.EmployeeId,
                    Date = DateTime.Now,
                    LeaveType = "Full Day",
                    Action = "Salary Deducted",
                    FreeLeavesLeft = currentFree,
                    PaidLeaves = paidFull,
                    Month = dto.Month,
                    Year = dto.Year
                });
            }
            if (paidHalf)
            {
                _context.LeaveAdjustmentHistories.Add(new LeaveAdjustmentHistory
                {
                    EmployeeID = dto.EmployeeId,
                    Date = DateTime.Now,
                    LeaveType = "Half Day",
                    Action = "Salary Deducted",
                    FreeLeavesLeft = currentFree,
                    PaidLeaves = 0.5m,
                    Month = dto.Month,
                    Year = dto.Year
                });
            }
            if (freeUsed == 0m && paidUsed == 0m)
            {
                _context.LeaveAdjustmentHistories.Add(new LeaveAdjustmentHistory
                {
                    EmployeeID = dto.EmployeeId,
                    Date = DateTime.Now,
                    LeaveType = "-",
                    Action = "No Deduction",
                    FreeLeavesLeft = currentFree,
                    PaidLeaves = 0,
                    Month = dto.Month,
                    Year = dto.Year
                });
            }
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Salary adjustment saved." });
        }


        [HttpPost]
        public async Task<IActionResult> SendCelebrationEmailsEmpAsync()
        {
            var today = DateTime.Today;
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

            // 🎂 Employees having Birthday Today
            var birthdayEmployees = employees
                .Where(e => e.DateOfBirth != null &&
                            e.DateOfBirth.Day == today.Day &&
                            e.DateOfBirth.Month == today.Month)
                .ToList();

            // 🎊 Employees having Work Anniversary Today
            var anniversaryEmployees = employees
                .Where(e => e.JoiningDate != null &&
                            e.JoiningDate.Day == today.Day &&
                            e.JoiningDate.Month == today.Month)
                .ToList();
            string ccEmail = "rajatkhanna.netdeveloper@gmail.com";
            // 🎂 Send Birthday Emails
            foreach (var emp in birthdayEmployees)
            {
                string subject = $"🎂 Happy Birthday, {emp.FullName}!";
                string body = GetEmailTemplate(emp.FullName, "birthday-today");
            
                await _emailService.SendEmailAsync(emp.Email, subject, body,ccEmail); // Send to employee
            }

            // 🎊 Send Anniversary Emails
            foreach (var emp in anniversaryEmployees)
            {
                string subject = $"🎊 Happy Work Anniversary, {emp.FullName}!";
                string body = GetEmailTemplate(emp.FullName, "anniversary-today");
                await _emailService.SendEmailAsync(emp.Email, subject, body,ccEmail); // Send to employee
            }

            return Ok("Today's celebration emails sent successfully!");
        }
        private string GetEmailTemplate(string name, string type)
        {
            var sb = new StringBuilder();

            // Email container with gradient background
            sb.Append("<div style='font-family:\"Segoe UI\",Arial,sans-serif;padding:40px 20px;'>");
            sb.Append("<div style='background:#fff;border-radius:16px;padding:0;max-width:600px;margin:auto;overflow:hidden;box-shadow:0 10px 40px rgba(0,0,0,0.2);'>");

            // Header section with gradient
            if (type == "birthday-today")
            {
                sb.Append("<div style='background:linear-gradient(135deg,#f093fb 0%,#f5576c 100%);padding:40px 30px;text-align:center;'>");
           
                sb.Append("<h1 style='color:#fff;margin:20px 0 10px 0;font-size:32px;font-weight:700;text-shadow:2px 2px 4px rgba(0,0,0,0.2);'>🎉 Happy Birthday! 🎉</h1>");
                sb.Append($"<h2 style='color:#fff;margin:0;font-size:28px;font-weight:600;text-shadow:1px 1px 3px rgba(0,0,0,0.2);'>{name}</h2>");
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<div style='background:linear-gradient(135deg,#4facfe 0%,#00f2fe 100%);padding:40px 30px;text-align:center;'>");
       
                sb.Append("<h1 style='color:#fff;margin:20px 0 10px 0;font-size:32px;font-weight:700;text-shadow:2px 2px 4px rgba(0,0,0,0.2);'>🎊 Happy Work Anniversary! 🎊</h1>");
                sb.Append($"<h2 style='color:#fff;margin:0;font-size:28px;font-weight:600;text-shadow:1px 1px 3px rgba(0,0,0,0.2);'>{name}</h2>");
                sb.Append("</div>");
            }

            // Main content
            sb.Append("<div style='padding:40px 30px;'>");

            if (type == "birthday-today")
            {
                sb.Append("<p style='font-size:18px;line-height:1.8;color:#333;text-align:center;margin:0 0 20px 0;'>");
                sb.Append("🎂 Today is <strong>your special day</strong>, and we want you to know how much you mean to us!");
                sb.Append("</p>");

                sb.Append("<p style='font-size:16px;line-height:1.8;color:#555;text-align:center;margin:0 0 25px 0;'>");
                sb.Append("May this year bring you endless joy, amazing opportunities, and all the success you deserve. 🌟");
                sb.Append("</p>");

                sb.Append("<div style='background:linear-gradient(135deg,#ffecd2 0%,#fcb69f 100%);border-radius:12px;padding:25px;margin:25px 0;text-align:center;'>");
                sb.Append("<p style='font-size:20px;color:#d63031;margin:0;font-weight:600;'>🎁 \"Celebrate every moment!\" 🎁</p>");
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<p style='font-size:18px;line-height:1.8;color:#333;text-align:center;margin:0 0 20px 0;'>");
                sb.Append("🌟 Congratulations on reaching this <strong>incredible milestone</strong> with us!");
                sb.Append("</p>");

                sb.Append("<p style='font-size:16px;line-height:1.8;color:#555;text-align:center;margin:0 0 25px 0;'>");
                sb.Append("Your dedication, hard work, and commitment have been truly inspiring. Thank you for being an amazing part of our team! 💼");
                sb.Append("</p>");

                sb.Append("<div style='background:linear-gradient(135deg,#a8edea 0%,#fed6e3 100%);border-radius:12px;padding:25px;margin:25px 0;text-align:center;'>");
                sb.Append("<p style='font-size:20px;color:#0984e3;margin:0;font-weight:600;'>🏆 \"Here's to many more years of success!\" 🏆</p>");
                sb.Append("</div>");
            }

            sb.Append("<p style='font-size:16px;line-height:1.8;color:#555;text-align:center;margin:25px 0 0 0;'>");
            sb.Append("Wishing you a fantastic day ahead filled with happiness and wonderful memories! ✨");
            sb.Append("</p>");

            sb.Append("</div>");

            // Footer
            sb.Append("<div style='background:#f8f9fa;padding:25px 30px;border-top:3px solid #e9ecef;text-align:center;'>");
            sb.Append("<p style='color:#6c757d;font-size:14px;margin:0 0 8px 0;'>Sent with ❤️ by</p>");
            sb.Append("<p style='color:#495057;font-size:16px;font-weight:600;margin:0;'>Business Box Help</p>");
            sb.Append("</div>");

            sb.Append("</div>");
            sb.Append("</div>");

            return sb.ToString();
        }

        public async Task SendUpcomingCelebrationEmailsToMangerAsync()
        {
            var tomorrow = DateTime.Today.AddDays(1);

            // Get all employees
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

            // 🎂 Employees having Birthday Tomorrow
            var birthdayEmployees = employees
                .Where(e => e.DateOfBirth != null &&
                            e.DateOfBirth.Day == tomorrow.Day &&
                            e.DateOfBirth.Month == tomorrow.Month)
                .ToList();

            // 🎊 Employees having Work Anniversary Tomorrow
            var anniversaryEmployees = employees
                .Where(e => e.JoiningDate != null &&
                            e.JoiningDate.Day == tomorrow.Day &&
                            e.JoiningDate.Month == tomorrow.Month)
                .ToList();

            // ✅ If any upcoming celebrations exist
            if (birthdayEmployees.Any() || anniversaryEmployees.Any())
            {
                string subject = "🎉 Team Celebration Reminder - Tomorrow";
                string body = GetManagerReminderTemplate(birthdayEmployees, anniversaryEmployees, tomorrow);

                // 📧 Send mail to manager
               await _emailService.SendEmailAsync("rajatkhanna.netdeveloper@gmail.com", subject, body);
               // await _emailService.SendEmailAsync("amandhiman.businessBox@gmail.com", subject, body);
            }
        }

        private string GetManagerReminderTemplate(List<ApplicationUser> birthdayEmployees, List<ApplicationUser> anniversaryEmployees, DateTime celebrationDate)
        {
            var sb = new StringBuilder();

            // Email container
            sb.Append("<div style='font-family:\"Segoe UI\",Arial,sans-serif;padding:20px;'>");
            sb.Append("<div style='background:#fff;border-radius:16px;padding:0;max-width:650px;margin:auto;overflow:hidden;box-shadow:0 10px 40px rgba(0,0,0,0.1);border:1px solid #e0e0e0;'>");

            // Header with gradient
            sb.Append("<div style='background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:40px 30px;text-align:center;'>");
            sb.Append("<h1 style='color:#fff;margin:0 0 10px 0;font-size:28px;font-weight:700;text-shadow:2px 2px 4px rgba(0,0,0,0.2);'>🎉 Team Celebration Reminder</h1>");
            sb.Append($"<p style='color:#fff;margin:0;font-size:16px;opacity:0.95;'>{celebrationDate:dddd, MMMM dd, yyyy}</p>");
            sb.Append("</div>");

            // Main content
            sb.Append("<div style='padding:35px 30px;'>");

            sb.Append("<p style='font-size:17px;line-height:1.7;color:#333;margin:0 0 25px 0;'>");
            sb.Append("Hello Manager,");
            sb.Append("</p>");

            sb.Append("<p style='font-size:16px;line-height:1.7;color:#555;margin:0 0 30px 0;'>");
            sb.Append("We have some special celebrations coming up <strong>tomorrow</strong>! Here are the team members who will be celebrating:");
            sb.Append("</p>");

            // Birthday Section
            if (birthdayEmployees.Any())
            {
                sb.Append("<div style='background:linear-gradient(135deg,#fff5f5 0%,#ffe0e0 100%);border-left:4px solid #f5576c;border-radius:8px;padding:20px 25px;margin:0 0 20px 0;'>");
                sb.Append("<h3 style='color:#d63031;margin:0 0 15px 0;font-size:18px;font-weight:600;'>🎂 Birthdays</h3>");

                foreach (var emp in birthdayEmployees)
                {
                    sb.Append("<div style='background:#fff;border-radius:6px;padding:12px 15px;margin:0 0 10px 0;display:flex;align-items:center;'>");
                    sb.Append($"<span style='color:#333;font-size:15px;font-weight:500;'>🎈 {emp.FullName}</span>");
                    sb.Append("</div>");
                }

                sb.Append("</div>");
            }

            // Anniversary Section
            if (anniversaryEmployees.Any())
            {
                sb.Append("<div style='background:linear-gradient(135deg,#f0f8ff 0%,#e0f0ff 100%);border-left:4px solid #00f2fe;border-radius:8px;padding:20px 25px;margin:0 0 20px 0;'>");
                sb.Append("<h3 style='color:#0984e3;margin:0 0 15px 0;font-size:18px;font-weight:600;'>🎊 Work Anniversaries</h3>");

                foreach (var emp in anniversaryEmployees)
                {
                    int yearsCompleted = celebrationDate.Year - emp.JoiningDate.Year;
                    sb.Append("<div style='background:#fff;border-radius:6px;padding:12px 15px;margin:0 0 10px 0;display:flex;align-items:center;justify-content:space-between;'>");
                    sb.Append($"<span style='color:#333;font-size:15px;font-weight:500;'>🏆 {emp.FullName}</span>");
                    sb.Append($"<span style='color:#0984e3;font-size:13px;font-weight:600;background:#e0f0ff;padding:4px 12px;border-radius:12px;'>{yearsCompleted} {(yearsCompleted == 1 ? "Year" : "Years")}</span>");
                    sb.Append("</div>");
                }

                sb.Append("</div>");
            }

            // Call to action
            sb.Append("<div style='background:#fff9e6;border-radius:10px;padding:20px;margin:25px 0 0 0;border:2px dashed #ffa500;text-align:center;'>");
            sb.Append("<p style='color:#ff8c00;font-size:15px;margin:0;font-weight:600;'>💡 Don't forget to wish them and make their day special!</p>");
            sb.Append("</div>");

            sb.Append("</div>");

            // Footer
            sb.Append("<div style='background:#f8f9fa;padding:20px 30px;border-top:1px solid #e9ecef;'>");
            sb.Append("<p style='color:#6c757d;font-size:13px;margin:0;text-align:center;'>This is an automated reminder from <strong>Business Box Help</strong></p>");
            sb.Append("<p style='color:#adb5bd;font-size:12px;margin:5px 0 0 0;text-align:center;'>Leave Management System</p>");
            sb.Append("</div>");

            sb.Append("</div>");
            sb.Append("</div>");

            return sb.ToString();
        }

        public IActionResult TodayAttendanceReport()
        {

            var today = DateTime.Today;

            var users = _context.Users.Where(u => u.Role == "Employee").ToList();

            var todayData = users
                .Select(user => new TodayAttendanceViewModel
                {
                    UserId = Guid.Parse(user.Id),   // Assuming Id is string
                    UserName = user.FullName,
                    AttendanceHistory = _context.tblAttendances
                        .Where(a => a.UserId.ToString() == user.Id && a.CheckedInTime.Value.Date == today)
                        .Select(a => new AttendanceReportViewModel
                        {
                            CheckedInTime = (DateTime)a.CheckedInTime,
                            CheckedOutTime = a.CheckedoutTime,
                            CheckedInImage = a.CheckedinImage,
                            CheckedOutImage = a.CheckedoutImage,
                            WorkingHours = a.CheckedoutTime != null
                                ? ((a.CheckedoutTime.Value - a.CheckedInTime.Value) - TimeSpan.FromMinutes(45)).TotalHours
                                : (double?)null
                        })
                        .ToList()
                })
                .ToList();

            return View(todayData);
            //        var today = DateTime.Today;

            //        var todayData = _context.tblAttendances
            //            .Where(a => a.CheckedInTime.Value.Date == today)
            //            .Select(a => new
            //            {
            //                a.UserId,
            //                a.CheckedInTime,
            //                a.CheckedoutTime,
            //                a.CheckedinImage,
            //                a.CheckedoutImage,
            //                UserName = _context.Users
            //                    .Where(u => u.Id == a.UserId.ToString())
            //                    .Select(u => u.FullName)
            //                    .FirstOrDefault()
            //            })
            //            .AsEnumerable()
            //            .GroupBy(a => new { a.UserId, a.UserName })
            //            .Select(g => new TodayAttendanceViewModel
            //            {
            //                UserId = g.Key.UserId,
            //                UserName = g.Key.UserName,
            //                AttendanceHistory = g.Select(x => new AttendanceReportViewModel
            //                {
            //                    CheckedInTime = (DateTime)x.CheckedInTime,
            //                    CheckedOutTime = x.CheckedoutTime,
            //                    CheckedInImage = x.CheckedinImage,
            //                    CheckedOutImage = x.CheckedoutImage,
            //                    WorkingHours = x.CheckedoutTime != null
            //? ((x.CheckedoutTime.Value - x.CheckedInTime.Value) - TimeSpan.FromMinutes(45)).TotalHours : (double?)null
            //                }).ToList()
            //            })
            //            .ToList();

            //        return View(todayData);
        }
        public IActionResult UserAttendance(Guid userId)
        {
            var userName = _context.Users
                .Where(u => u.Id == userId.ToString())
                .Select(u => u.FullName)
                .FirstOrDefault();

            ViewBag.UserName = userName;
            var report = _context.tblAttendances
               .Where(a => a.UserId == userId)
               .OrderByDescending(a => a.CheckedInTime)
               .Select(a => new AttendanceReportViewModel
               {
                   CheckedInTime = a.CheckedInTime.Value,
                   CheckedOutTime = a.CheckedoutTime,
                   CheckedInImage = a.CheckedinImage,
                   CheckedOutImage = a.CheckedoutImage,
                   WorkingHours = a.CheckedoutTime != null
                        ? ((a.CheckedoutTime.Value - a.CheckedInTime.Value) - TimeSpan.FromMinutes(45)).TotalHours
                        : (double?)null
               })
                .ToList();

            return View(report);
        }


        public async Task<IActionResult> Create()
        {
            await SetUserInfoAsync();
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(ApplicationUser model, decimal baseSalary)
        {
            await SetUserInfoAsync();
            if (ModelState.IsValid)
            {
                model.BaseSalary = baseSalary;
                model.UserName = model.Email;
                model.EmailConfirmed = true;
                model.YearlyFreeLeaves = model.YearlyFreeLeaves; // from form
                model.FreeLeavesLeft = model.YearlyFreeLeaves;
                var result = await _userManager.CreateAsync(model, "Default@123");

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(model, model.Role);
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        public async Task<IActionResult> SalaryList()
        {
            await SetUserInfoAsync();
            await GenerateCurrentMonthSalaryReports();
            var today = DateTime.Now;
            var month = today.Month;
            var salaryList = await _context.SalaryReports
             .Where(l => l.Month <= month && l.IsPaid == false)
             .Include(l => l.User)
             .ToListAsync();
            return View(salaryList);

        }

        public async Task<IActionResult> SalaryApprove(int Id)
        {
            await SetUserInfoAsync();
            if (Id != null)
            {
                var data = await _context.SalaryReports.FindAsync(Id);
                if (data != null)
                {
                    data.IsPaid = true;
                    _context.SalaryReports.Update(data);
                    _context.SaveChangesAsync();

                    return RedirectToAction("SalaryList");
                }

                return RedirectToAction("SalaryList");
            }
            return RedirectToAction("SalaryList");
        }
        [HttpGet]
        public async Task<IActionResult> SalaryDetail(string id)
        {
            await SetUserInfoAsync();
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentYear = today.Year;

            var previousDate = today.AddMonths(-1);
            var prevMonth = previousDate.Month;
            var prevYear = previousDate.Year;

            var prevUnpaid = await _context.SalaryReports
                .Include(r => r.User)
                .Where(r => r.UserId == id && !r.IsPaid &&
                            r.Month == prevMonth && r.Year == prevYear)
                .FirstOrDefaultAsync();

            if (prevUnpaid != null)
                return View(prevUnpaid);

            var currentUnpaid = await _context.SalaryReports
                .Include(r => r.User)
                .Where(r => r.UserId == id && !r.IsPaid &&
                            r.Month == currentMonth && r.Year == currentYear)
                .FirstOrDefaultAsync();

            if (currentUnpaid != null)
                return View(currentUnpaid);

            return View(null);
        }

   

        [HttpGet]

        public async Task<IActionResult> AddLeave()
        {
            await SetUserInfoAsync();
            var employeeRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Employee");


            var employees = await (from user in _context.Users
                                   join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                   where userRole.RoleId == employeeRole.Id
                                   select user).ToListAsync();

            ViewBag.Employees = employees;
            ViewBag.LeaveTypes = await _context.LeaveTypes.ToListAsync();

            return View();
        }   

        [HttpPost]
        public async Task<IActionResult> ApplyLeave(LeaveRequest model)
        {
            await SetUserInfoAsync();
            model.Status = LeaveStatus.Approved;
            model.AppliedOn = DateTime.UtcNow;


            var overlapExists = await _context.LeaveRequests.AnyAsync(l =>
                l.UserId == model.UserId &&
                l.Status != LeaveStatus.Rejected &&
                (
                    (model.StartDate >= l.StartDate && model.StartDate <= l.EndDate) ||
                    (model.EndDate >= l.StartDate && model.EndDate <= l.EndDate) ||
                    (model.StartDate <= l.StartDate && model.EndDate >= l.EndDate)
                )
            );

            if (overlapExists)
            {
                ModelState.AddModelError("", "A leave request already exists within the selected date range for this user.");
            }
            if (model.StartDate > model.EndDate)
            {
                ModelState.AddModelError("", "Start Date should be before End Date.");
            }

            if (!ModelState.IsValid)
            {
                var employeeRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Employee");
                var employees = await (from user in _context.Users
                                       join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                       where userRole.RoleId == employeeRole.Id
                                       select user).ToListAsync();

                ViewBag.Employees = employees;
                ViewBag.LeaveTypes = await _context.LeaveTypes.ToListAsync();
                return View("AddLeave", model);
            }

            _context.LeaveRequests.Add(model);
            await _context.SaveChangesAsync();
            //await CreateOrUpdateSalaryReportFromLeave(model.UserId, model.StartDate, model.EndDate, model.IsHalfDay);
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.UserId);
            model.LeaveType = await _context.LeaveTypes.FirstOrDefaultAsync(l => l.Id == model.LeaveTypeId);

            if (employee != null )
            {
                await SendLeaveAppliedByManagerEmailAsync(employee, model);
            }


            TempData["Success"] = "Leave request submitted successfully!";
            return RedirectToAction("AddLeave");
        }
        private async Task SendLeaveAppliedByManagerEmailAsync(ApplicationUser employee, LeaveRequest model)
        {
            string managerName = "Manager"; // static
            string dayType = model.IsHalfDay ? "Half Day" : "Full Day";

            string subject = $"✅ Leave Applied by {managerName}";

            string body = $@"
        <html>
        <head>
            <style>
                body {{
                    font-family: 'Segoe UI', sans-serif;
                    background-color: #f8f9fa;
                    margin: 0;
                    padding: 0;
                }}
                .email-container {{
                    max-width: 600px;
                    margin: 30px auto;
                    background-color: #ffffff;
                    border-radius: 10px;
                    box-shadow: 0 4px 10px rgba(0,0,0,0.1);
                    padding: 20px 30px;
                }}
                .header {{
                    background-color: #28a745;
                    color: white;
                    padding: 12px 20px;
                    border-radius: 8px 8px 0 0;
                    text-align: center;
                    font-size: 20px;
                    font-weight: 600;
                }}
                .content {{
                    margin: 20px 0;
                    color: #333333;
                    line-height: 1.6;
                }}
                .content strong {{
                    color: #28a745;
                }}
                .footer {{
                    text-align: center;
                    font-size: 12px;
                    color: #999999;
                    margin-top: 25px;
                    border-top: 1px solid #eaeaea;
                    padding-top: 10px;
                }}
                .highlight {{
                    background-color: #f1fff5;
                    border-left: 4px solid #28a745;
                    padding: 8px 12px;
                    margin: 10px 0;
                    border-radius: 4px;
                }}
            </style>
        </head>
        <body>
            <div class='email-container'>
                <div class='header'>Leave Applied by Manager</div>
                <div class='content'>
                    <p>Hello <strong>{employee.FullName}</strong>,</p>
                    <p>Your  <strong>{managerName}</strong> has applied a leave request on your behalf.</p>

                    <div class='highlight'>
                        <p><strong>Leave Type:</strong> {model.LeaveType?.Name ?? "N/A"}</p>
                        <p><strong>Duration:</strong> {model.StartDate:dd MMM yyyy} - {model.EndDate:dd MMM yyyy}</p>
                        <p><strong>Day Type:</strong> {dayType}</p>
                        <p><strong>Reason:</strong> {model.Reason ?? "Not specified"}</p>
                        <p><strong>Status:</strong> {model.Status}</p>
                    </div>

                    <p>
                        You can review the details in your <strong>Business Box Leave Management</strong> panel.
                    </p>
                </div>
                <div class='footer'>
                    <p>This is an automated message from <strong>Business Box Leave Management System</strong>.</p>
                </div>
            </div>
        </body>
        </html>
    ";

            await _emailService.SendEmailAsync(employee.Email, subject, body);
        }
        public async Task<IActionResult> Edit(string id)
        {
            await SetUserInfoAsync();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ApplicationUser model, decimal baseSalary)
        {
            await SetUserInfoAsync();
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.JoiningDate = model.JoiningDate;
                user.BaseSalary = baseSalary;
                user.Role = model.Role;
                user.PhoneNumber = model.PhoneNumber;
                user.IsActive = model.IsActive;
                user.DateOfBirth = model.DateOfBirth;
                user.Email = model.Email;
                user.YearlyFreeLeaves = model.YearlyFreeLeaves;
                user.FreeLeavesLeft = model.FreeLeavesLeft;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                    return RedirectToAction("Index");

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }


        public async Task<IActionResult> Delete(string id)
        {
            await SetUserInfoAsync();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> ApprovedLeaves()
        {
            await SetUserInfoAsync();
            var leaves = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveStatus.Approved)
                .OrderByDescending(lr => lr.StartDate)               
                .ToListAsync();

            return View(leaves);
        }

        [HttpGet]
        public async Task<IActionResult> RejectedLeaves()
        {
            await SetUserInfoAsync();
            var leaves = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveStatus.Rejected)
                .OrderByDescending(lr => lr.AppliedOn)
                .ToListAsync();

            return View(leaves);
        }

        [HttpGet]
        public async Task<IActionResult> PendingLeaves()
        {
            await SetUserInfoAsync();
            var leaves = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveStatus.Pending)
                .OrderByDescending(lr => lr.AppliedOn)
                .ToListAsync();

            return View(leaves);
        }

        [HttpGet]
        public async Task<IActionResult> LeaveByUser(string id)
        {
            await SetUserInfoAsync();
            ViewBag.UserId = id;
            var leaveRequests = await _context.LeaveRequests
                .Where(lr => lr.UserId == id)
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                    .OrderBy(lr => lr.StartDate)
                .ToListAsync();

            return View(leaveRequests);
        }
        [HttpGet]
        public async Task<IActionResult> UpdateLeaveStatus(int id)
        {
            await SetUserInfoAsync();
            var request = await _context.LeaveRequests
                .Include(r => r.User)
                .Include(r => r.LeaveType)
                .FirstOrDefaultAsync(r => r.Id == id);
          
            if (request == null) return NotFound();

            return View(request);
        }
        [HttpGet]
        public async Task<IActionResult> DeleteLeave(int id)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();

            if (leave.Status == LeaveStatus.Approved)
            {
                var user = await _userManager.FindByIdAsync(leave.UserId);
                if (user == null) return NotFound();

                DateTime current = leave.StartDate.Date;
                DateTime end = leave.EndDate.Date;

                while (current <= end)
                {
                    var year = current.Year;
                    var month = current.Month;

                    var monthStart = new DateTime(year, month, 1);
                    var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

                    var leaveStart = current > monthStart ? current : monthStart;
                    var leaveEnd = end < monthEnd ? end : monthEnd;

                    int totalDaysInMonth = DateTime.DaysInMonth(year, month);
                    int joiningDay = (user.JoiningDate.Year == year && user.JoiningDate.Month == month)
                                        ? user.JoiningDate.Day
                                        : 1;
                    int workingDays = totalDaysInMonth - (joiningDay - 1);

                    decimal perDaySalary = user.BaseSalary / totalDaysInMonth;

                    int fullDays = (leaveEnd - leaveStart).Days + 1;
                    double leaveDaysInThisMonth = leave.IsHalfDay ? fullDays * 0.5 : fullDays;

                    decimal deductionToRemove = (decimal)leaveDaysInThisMonth * perDaySalary;

                    var report = await _context.SalaryReports
                        .FirstOrDefaultAsync(r => r.UserId == leave.UserId && r.Month == month && r.Year == year);

                    if (report != null)
                    {
                        report.LeaveTakenThisMonth -= (float)leaveDaysInThisMonth;
                        if (report.LeaveTakenThisMonth < 0)
                            report.LeaveTakenThisMonth = 0;

                        report.Deductions -= deductionToRemove;
                        if (report.Deductions < 0)
                            report.Deductions = 0;

                        report.TotalWorkingDays = workingDays;
                        report.BaseSalary = Math.Round(perDaySalary * workingDays, 2);
                    }

                    current = leaveEnd.AddDays(1);
                }
            }

            _context.LeaveRequests.Remove(leave);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Leave request deleted successfully!";
            return RedirectToAction("ApprovedLeaves");
        }


        [HttpPost]

        public async Task<IActionResult> UpdateLeaveStatus(LeaveRequest model)
        {
            await SetUserInfoAsync();
            var request = await _context.LeaveRequests.Include(l => l.User)       
                    .Include(l => l.LeaveType).FirstOrDefaultAsync(l => l.Id == model.Id);
            if (request == null) return NotFound();
                

            if (request.Status == LeaveStatus.Pending)
            {
                request.Status = model.Status;  
                await _context.SaveChangesAsync();
                await CreateOrUpdateSalaryReportFromLeave(request.UserId, model.StartDate, model.EndDate, model.IsHalfDay);

                try
                {
                        await SendLeaveStatusEmailAsync(request);
                    Console.WriteLine($"📧 Leave status email sent to {request.User.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to send leave status email: {ex.Message}");
                }
            }

            return RedirectToAction("Index");
        }
        private async Task SendLeaveStatusEmailAsync(LeaveRequest request)
        {
            string subject = $"📢 Update on Your Leave Request ({request.StartDate:dd MMM} - {request.EndDate:dd MMM})";

            string statusColor = request.Status == LeaveStatus.Approved ? "#28a745" : "#dc3545";
            string statusText = request.Status.ToString();

            string body = $@"
                            <html>
                            <head>
                                <style>
                                    body {{
                                        font-family: 'Segoe UI', sans-serif;
                                        background-color: #f4f4f7;
                                        margin: 0;
                                        padding: 0;
                                    }}
                                    .email-container {{
                                        max-width: 600px;
                                        margin: 30px auto;
                                        background-color: #fff;
                                        border-radius: 10px;
                                        box-shadow: 0 4px 10px rgba(0,0,0,0.1);
                                        overflow: hidden;
                                    }}
                                    .header {{
                                        background-color: #007bff;
                                        color: white;
                                        text-align: center;
                                        padding: 14px;
                                        font-size: 20px;
                                        font-weight: 600;
                                    }}
                                    .content {{
                                        padding: 25px;
                                        color: #333;
                                        line-height: 1.6;
                                    }}
                                    .highlight {{
                                        background-color: #f9fbfd;
                                        border-left: 4px solid #007bff;
                                        padding: 10px 15px;
                                        margin: 15px 0;
                                        border-radius: 4px;
                                    }}
                                    .status {{
                                        color: {statusColor};
                                        font-weight: bold;
                                    }}
                                    .footer {{
                                        background-color: #f1f1f1;
                                        text-align: center;
                                        font-size: 12px;
                                        color: #666;
                                        padding: 10px;
                                    }}
                                </style>
                            </head>
                            <body>
                                <div class='email-container'>
                                    <div class='header'>Leave Request Update</div>
                                    <div class='content'>
                                        <p>Dear <strong>{request.User.FullName}</strong>,</p>
                                        <p>Your leave request has been <span class='status'>{statusText}</span>.</p>

                                        <div class='highlight'>
                                            <p><strong>Leave Type:</strong> {request.LeaveType?.Name}</p>
                                            <p><strong>Duration:</strong> {request.StartDate:dd MMM yyyy} - {request.EndDate:dd MMM yyyy}</p>
                                            <p><strong>Reason:</strong> {request.Reason}</p>
                                            <p><strong>Status:</strong> <span class='status'>{statusText}</span></p>
                                        </div>

                                        <p>
                                            If you have any questions regarding this update, please contact your manager.
                                        </p>
                                    </div>
                                    <div class='footer'>
                                        <p>This is an automated message from <strong>Business Box Leave Management System</strong>.<br/>
                                        Please do not reply to this email — this mailbox is not monitored.</p>
                                    </div>
                                </div>
                            </body>
                            </html>";

            await _emailService.SendEmailAsync(request.User.Email, subject, body);
        }

        [HttpGet]
        public async Task<IActionResult> RemainingLeaves()
        {
            int currentYear = DateTime.Now.Year;

            // 1) Get all employees
            var users = await _userManager.GetUsersInRoleAsync("Employee");
            var userIds = users.Select(u => u.Id).ToList();

            // 2) Fetch all salary adjustments for current year (ONE QUERY)
            var adjustments = await _context.SalaryAdjustments
                .Where(x => userIds.Contains(x.EmployeeID) && x.Year == currentYear)
                .ToListAsync();

            var summaries = new List<UserLeaveSummaryViewModel>();

            foreach (var user in users)
            {
                var userAdjustments = adjustments
                    .Where(x => x.EmployeeID == user.Id)
                    .ToList();

                double paidUsed = userAdjustments.Sum(x => (double)x.PaidLeavesDeducted);
                double freeUsed = userAdjustments.Sum(x => (double)x.FreeLeavesUsed);
                double totalUsed = userAdjustments.Sum(x => (double)x.LeavesTaken);

                double remainingFree = Convert.ToDouble(user.FreeLeavesLeft.GetValueOrDefault());

                summaries.Add(new UserLeaveSummaryViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    LeaveTypeName = "Annual Leave",

                    TotalAllocated = user.YearlyFreeLeaves,
                    Used = Math.Round(totalUsed, 1),
                    PaidUsed = Math.Round(paidUsed, 1),
                    FreeUsed = Math.Round(freeUsed, 1),
                    Remaining = Math.Round(remainingFree, 1)
                });
            }

            return View(summaries);
        }



        public async Task CheckRemainingLeavesAndNotifyAsync()
        {
            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            int currentYear = today.Year;

            var startOfYear = new DateTime(currentYear, 1, 1);
            var endOfCurrentMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

            var users = await _userManager.GetUsersInRoleAsync("Employee");

            foreach (var user in users)
            {
                var approvedLeaves = await _context.LeaveRequests
                    .Where(l => l.UserId == user.Id &&
                                l.Status == LeaveStatus.Approved &&
                                l.EndDate >= startOfYear &&
                                l.StartDate <= endOfCurrentMonth)
                    .ToListAsync();

                var leaveDays = new List<(DateTime date, bool isHalfDay)>();

                foreach (var leave in approvedLeaves)
                {
                    var leaveStart = leave.StartDate < startOfYear ? startOfYear : leave.StartDate;
                    var leaveEnd = leave.EndDate > endOfCurrentMonth ? endOfCurrentMonth : leave.EndDate;

                    for (var dt = leaveStart; dt <= leaveEnd; dt = dt.AddDays(1))
                    {
                        if (dt.Year == currentYear && dt.Month <= currentMonth)
                        {
                            leaveDays.Add((dt, leave.IsHalfDay));
                        }
                    }
                }

                var leavesByMonth = leaveDays
                    .GroupBy(ld => ld.date.Month)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToList()
                    );

                double carryForward = 0;
                double totalPaidUsed = 0;

                for (int month = 1; month <= currentMonth; month++)
                {
                    double allowed = 1;
                    double takenThisMonth = leavesByMonth.ContainsKey(month)
                        ? leavesByMonth[month].Sum(ld => ld.isHalfDay ? 0.5 : 1)
                        : 0;

                    double available = allowed + carryForward;
                    double paidUsed = Math.Min(takenThisMonth, available);

                    carryForward = Math.Max(0, available - paidUsed);
                    totalPaidUsed += paidUsed;
                }

                double remaining = Math.Round(carryForward, 1);

                if (remaining <= 0)
                {
                    await SendNoLeaveBalanceEmailAsync(user);
                }
            }
        }
        private async Task SendNoLeaveBalanceEmailAsync(ApplicationUser user)
        {
          string managerEmail = "rajatkhanna.netdeveloper@gmail.com"; // 📩 static manager email
           //string managerEmail = "amandhiman.businessBox@gmail.com"; // 📩 static manager email

            // --------------------------------------------------
            // 🧍‍♀️ Employee Email Template
            // --------------------------------------------------
            string employeeSubject = $"⚠️ Leave Balance Alert – {user.FullName}";
            string employeeBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: 'Segoe UI', Arial, sans-serif;
                                                background-color: #f7f9fb;
                                                margin: 0;
                                                padding: 0;
                                            }}
                                            .email-container {{
                                                max-width: 600px;
                                                margin: 30px auto;
                                                background-color: #ffffff;
                                                border-radius: 12px;
                                                box-shadow: 0 4px 12px rgba(0,0,0,0.08);
                                                overflow: hidden;
                                            }}
                                            .header {{
                                                background-color: #dc3545;
                                                color: #ffffff;
                                                text-align: center;
                                                padding: 18px;
                                                font-size: 20px;
                                                font-weight: 600;
                                            }}
                                            .content {{
                                                padding: 25px;
                                                color: #333333;
                                                line-height: 1.7;
                                            }}
                                            .highlight {{
                                                background-color: #fff5f5;
                                                border-left: 5px solid #dc3545;
                                                padding: 12px;
                                                border-radius: 6px;
                                                margin-top: 15px;
                                            }}
                                            .footer {{
                                                background-color: #f1f1f1;
                                                color: #888888;
                                                font-size: 12px;
                                                text-align: center;
                                                padding: 12px;
                                            }}
                                            a.button {{
                                                display: inline-block;
                                                margin-top: 15px;
                                                padding: 10px 18px;
                                                background-color: #007bff;
                                                color: #fff;
                                                border-radius: 6px;
                                                text-decoration: none;
                                                font-weight: 500;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='email-container'>
                                            <div class='header'>Leave Balance Alert</div>
                                            <div class='content'>
                                                <p>Dear <strong>{user.FullName}</strong>,</p>
                                                <p>We wanted to inform you that your <strong>annual leave balance</strong> has now reached <strong>0 days</strong>.</p>

                                                <div class='highlight'>
                                                    <p><strong>Employee Name:</strong> {user.FullName}</p>
                                                    <p><strong>Email:</strong> <a href='mailto:{user.Email}'>{user.Email}</a></p>
                                                    <p><strong>Remaining Leave:</strong> 0 days</p>
                                                    <p><strong>Status:</strong> No remaining paid leaves</p>
                                                </div>

                                                <p>
                                                    You have utilised all your paid leaves for the current year.
                                                    Any future requests may be treated as <strong>unpaid leave</strong> unless additional days are approved by your manager.
                                                </p>

                                                <p>For any clarification, please reach out to your reporting manager.</p>

                                                <a href='https://leavemanagement.businessbox.in/' class='button'>Open Leave Portal</a>
                                            </div>
                                            <div class='footer'>
                                                <p>Please do not reply to this email — this mailbox is not monitored.</p>
                                                <p>This is an automated message from <strong>Business Box Leave Management System</strong>.</p>
                                            </div>
                                        </div>
                                    </body>
                                    </html>";


            // --------------------------------------------------
            // 👨‍💼 Manager Email Template
            // --------------------------------------------------
            string managerSubject = $"📋 Leave Balance Exhausted – {user.FullName}";
            string managerBody = $@"
                                    <html>
                                    <head>
                                        <style>
                                            body {{
                                                font-family: 'Segoe UI', Arial, sans-serif;
                                                background-color: #f4f6f8;
                                                margin: 0;
                                                padding: 0;
                                            }}
                                            .email-container {{
                                                max-width: 600px;
                                                margin: 30px auto;
                                                background-color: #ffffff;
                                                border-radius: 10px;
                                                box-shadow: 0 4px 12px rgba(0,0,0,0.1);
                                                overflow: hidden;
                                            }}
                                            .header {{
                                                background-color: #007bff;
                                                color: #ffffff;
                                                text-align: center;
                                                padding: 18px;
                                                font-size: 20px;
                                                font-weight: 600;
                                            }}
                                            .content {{
                                                padding: 25px;
                                                color: #333333;
                                                line-height: 1.6;
                                            }}
                                            .highlight {{
                                                background-color: #eef4ff;
                                                border-left: 5px solid #007bff;
                                                padding: 12px;
                                                border-radius: 6px;
                                                margin-top: 15px;
                                            }}
                                            .footer {{
                                                background-color: #f1f1f1;
                                                color: #888888;
                                                font-size: 12px;
                                                text-align: center;
                                                padding: 12px;
                                            }}
                                             a.button {{
                                                display: inline-block;
                                                margin-top: 15px;
                                                padding: 10px 18px;
                                                background-color: #007bff;
                                                color: #fff;
                                                border-radius: 6px;
                                                text-decoration: none;
                                                font-weight: 500;
                                            }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='email-container'>
                                            <div class='header'>Employee Leave Balance Exhausted</div>
                                            <div class='content'>
                                                <p>Dear <strong>Manager</strong>,</p>
                                                <p>The following employee has used all of their allocated annual leaves for the current year:</p>

                                                <div class='highlight'>
                                                    <p><strong>Employee Name:</strong> {user.FullName}</p>
                                                    <p><strong>Email:</strong> <a href='mailto:{user.Email}'>{user.Email}</a></p>
                                                    <p><strong>Remaining Leave:</strong> 0 days</p>
                                                    <p><strong>Current Status:</strong> Fully utilised annual leave quota</p>
                                                </div>

                                                <p>
                                                    You may wish to review future leave applications for this employee as <strong>unpaid</strong> 
                                                    or consider additional leave allocation if justified.
                                                </p>

                                                <p>You can view full leave history on the Manager Panel.</p>
                                                <a href='https://leavemanagement.businessbox.in/' class='button'>Open Leave Portal</a>
                                            </div>
                                            <div class='footer'>
                                                <p>Please do not reply to this email — this mailbox is not monitored.</p>
                                                <p>This is an automated message from <strong>Business Box Leave Management System</strong>.</p>
                                            </div>
                                        </div>
                                    </body>
                                    </html>";

            // --------------------------------------------------
            // ✉️ Send both emails
            // --------------------------------------------------
            await _emailService.SendEmailAsync(user.Email, employeeSubject, employeeBody);
            await _emailService.SendEmailAsync(managerEmail, managerSubject, managerBody);

            Console.WriteLine($"📧 Leave balance alert sent to Employee ({user.Email}) and Manager ({managerEmail})");
        }
        public async Task NotifyManagerAboutEmployeesOnLeaveAsync()
        {
            var today = DateTime.UtcNow.Date;
          //  string managerEmail = "amandhiman.businessbox@gmail.com"; // ✅ static manager email
            string managerEmail = "rajatkhanna.netdeveloper@gmail.com"; // ✅ static manager email

            // Get all employees on approved leave for today
            var leavesToday = await _context.LeaveRequests
                .Where(l => l.Status == LeaveStatus.Approved &&
                            l.StartDate.Date <= today &&
                            l.EndDate.Date >= today)
                .Include(l => l.User)
                .ToListAsync();

            if (!leavesToday.Any())
            {
                
                return;
            }

            // Prepare email content
            var message = new StringBuilder();
            message.AppendLine("<h3>Employees on Leave Today:</h3>");
            message.AppendLine("<table border='1' cellspacing='0' cellpadding='6' style='border-collapse:collapse;width:100%;'>");
            message.AppendLine("<thead style='background-color:#f2f2f2;'>");
            message.AppendLine("<tr>");
            message.AppendLine("<th align='left'>Employee Name</th>");
            message.AppendLine("<th align='left'>Start Date</th>");
            message.AppendLine("<th align='left'>End Date</th>");
            message.AppendLine("<th align='left'>Type</th>");
            message.AppendLine("<th align='left'>Reason</th>");
            message.AppendLine("</tr>");
            message.AppendLine("</thead>");
            message.AppendLine("<tbody>");

            foreach (var leave in leavesToday)
            {
                string empName = $"{leave.User.FullName}";
                string start = leave.StartDate.ToString("dd MMM yyyy");
                string end = leave.EndDate.ToString("dd MMM yyyy");
                string type = leave.IsHalfDay ? "Half Day" : "Full Day";
                string reason = string.IsNullOrWhiteSpace(leave.Reason) ? "N/A" : leave.Reason;

                message.AppendLine("<tr>");
                message.AppendLine($"<td>{empName}</td>");
                message.AppendLine($"<td>{start}</td>");
                message.AppendLine($"<td>{end}</td>");
                message.AppendLine($"<td>{type}</td>");
                message.AppendLine($"<td>{reason}</td>");
                message.AppendLine("</tr>");
            }

            message.AppendLine("</tbody>");
            message.AppendLine("</table>");
            message.AppendLine($"<p><strong>Date:</strong> {today:dd MMM yyyy}</p>");
            message.AppendLine("<p>Regards,<br><strong>Leave Management System</strong></p>");

            // Send email
            await _emailService.SendEmailAsync(managerEmail, "Today's Leave Report", message.ToString());

            
        }





        #region Private

        private async Task GenerateCurrentMonthSalaryReports()
        {
            await SetUserInfoAsync();
            var currentDate = DateTime.UtcNow;
            int currentMonth = currentDate.Month;
            int currentYear = currentDate.Year;

            var endOfMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

            var employees = await (from user in _userManager.Users
                                   join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                   join role in _context.Roles on userRole.RoleId equals role.Id
                                   where role.Name == "Employee"
                                         && user.IsActive
                                         && user.JoiningDate <= endOfMonth
                                   select user).ToListAsync();

            foreach (var employee in employees)
            {
                var exists = await _context.SalaryReports
                    .FirstOrDefaultAsync(s => s.UserId == employee.Id && s.Month == currentMonth && s.Year == currentYear);

                if (exists != null)
                {

                    if (exists.BaseSalary == employee.BaseSalary)
                    {
                        continue;
                    }
                    else
                    {
                        // Salary update  → remove old report so new one will be generated
                        _context.SalaryReports.Remove(exists);
                    }
                }

                var joiningDay = employee.JoiningDate.Month == currentMonth && employee.JoiningDate.Year == currentYear
                    ? employee.JoiningDate.Day
                    : 1;

                int totalDaysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
                int totalWorkingDays = totalDaysInMonth - (joiningDay - 1);

                decimal perDaySalary = employee.BaseSalary / totalDaysInMonth;
                decimal proRatedBaseSalary = perDaySalary * totalWorkingDays;

                var approvedLeaves = await _context.LeaveRequests
                    .Where(l => l.UserId == employee.Id &&
                                l.Status == LeaveStatus.Approved &&
                                l.StartDate.Year == currentYear &&
                                l.StartDate.Month <= currentMonth)
                    .ToListAsync();

                double leaveTaken = 0;

                foreach (var leave in approvedLeaves)
                {
                    var leaveStart = leave.StartDate < new DateTime(currentYear, currentMonth, 1)
                        ? new DateTime(currentYear, currentMonth, 1)
                        : leave.StartDate;

                    var leaveEnd = leave.EndDate > new DateTime(currentYear, currentMonth, totalDaysInMonth)
                        ? new DateTime(currentYear, currentMonth, totalDaysInMonth)
                        : leave.EndDate;
                    if (leave.StartDate < new DateTime(currentYear, currentMonth, 1) && leave.EndDate < new DateTime(currentYear, currentMonth, 1))
                        continue;

                    int days = (leaveEnd - leaveStart).Days + 1;
                    double leaveDays = leave.IsHalfDay ? days * 0.5 : days;

                    leaveTaken += leaveDays;
                }
                    
                decimal deductions = (decimal)(leaveTaken > totalWorkingDays ? totalWorkingDays : leaveTaken) * perDaySalary;

                var salaryReport = new SalaryReport
                {
                    UserId = employee.Id,
                    Month = currentMonth,
                    Year = currentYear,
                    BaseSalary = Math.Round(proRatedBaseSalary, 2),
                    TotalWorkingDays = totalWorkingDays,
                    LeaveTakenThisMonth = (float)leaveTaken,
                    Deductions = Math.Round(deductions, 2),
                    Bonuses = 0,
                    IsPaid = false
                };

                _context.SalaryReports.Add(salaryReport);
            }

            await _context.SaveChangesAsync();
        }
        private async Task CreateOrUpdateSalaryReportFromLeave(string userId, DateTime leaveStartDate, DateTime leaveEndDate, bool isHalfDay)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive) return;

            var today = DateTime.UtcNow;
            var startOfYear = new DateTime(today.Year, 1, 1);
            var endOfToday = today;

            var allApprovedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == userId &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfYear &&
                            l.StartDate <= endOfToday &&
                            !(l.StartDate == leaveStartDate && l.EndDate == leaveEndDate))
                .ToListAsync();

            var currentLeaveDays = new List<(DateTime date, bool isHalfDay)>();
            for (var dt = leaveStartDate; dt <= leaveEndDate; dt = dt.AddDays(1))
            {
                currentLeaveDays.Add((dt, isHalfDay));
            }

            var combinedLeaveDays = new Dictionary<(int year, int month), List<(DateTime date, bool isHalfDay)>>();

            foreach (var item in currentLeaveDays)
            {
                var key = (item.date.Year, item.date.Month);
                if (!combinedLeaveDays.ContainsKey(key))
                    combinedLeaveDays[key] = new List<(DateTime, bool)>();

                combinedLeaveDays[key].Add(item);
            }

            foreach (var leave in allApprovedLeaves)
            {
                for (var dt = leave.StartDate; dt <= leave.EndDate; dt = dt.AddDays(1))
                {
                    var key = (dt.Year, dt.Month);
                    if (!combinedLeaveDays.ContainsKey(key))
                        combinedLeaveDays[key] = new List<(DateTime, bool)>();

                    combinedLeaveDays[key].Add((dt, leave.IsHalfDay));
                }
            }

            var orderedMonths = combinedLeaveDays.OrderBy(k => (k.Key.year, k.Key.month)).ToList();
            var paidLeaveUsedByYear = new Dictionary<int, double>();

            foreach (var monthGroup in orderedMonths)
            {
                int year = monthGroup.Key.year;
                int month = monthGroup.Key.month;
                var leaveDays = monthGroup.Value;

                double totalLeaveDaysThisMonth = leaveDays.Sum(x => x.isHalfDay ? 0.5 : 1);

                if (!paidLeaveUsedByYear.ContainsKey(year))
                    paidLeaveUsedByYear[year] = 0;

                double allowedPaidLeaveTillThisMonth = month;
                double availablePaidLeave = Math.Max(0, allowedPaidLeaveTillThisMonth - paidLeaveUsedByYear[year]);
                double paidLeave = Math.Min(availablePaidLeave, totalLeaveDaysThisMonth);
                double unpaidLeave = totalLeaveDaysThisMonth - paidLeave;

                paidLeaveUsedByYear[year] += paidLeave;

                int totalDaysInMonth = DateTime.DaysInMonth(year, month);
                int joiningDay = (user.JoiningDate.Year == year && user.JoiningDate.Month == month) ? user.JoiningDate.Day : 1;
                int totalWorkingDays = totalDaysInMonth - (joiningDay - 1);

                decimal perDaySalary = user.BaseSalary / totalDaysInMonth;
                decimal proRatedBaseSalary = perDaySalary * totalWorkingDays;
                decimal deductions = (decimal)unpaidLeave * perDaySalary;

                var report = await _context.SalaryReports
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.Year == year && r.Month == month);

                if (report == null)
                {
                    report = new SalaryReport
                    {
                        UserId = userId,
                        Year = year,
                        Month = month,
                        TotalWorkingDays = totalWorkingDays,
                        BaseSalary = Math.Round(proRatedBaseSalary, 2),
                        LeaveTakenThisMonth = (float)Math.Round(totalLeaveDaysThisMonth, 2),
                        Deductions = Math.Round(deductions, 2),
                        Bonuses = 0,
                        IsPaid = false
                    };
                    _context.SalaryReports.Add(report);
                }
                else
                {
                    report.TotalWorkingDays = totalWorkingDays;
                    report.BaseSalary = Math.Round(proRatedBaseSalary, 2);
                    report.LeaveTakenThisMonth = (float)Math.Round(totalLeaveDaysThisMonth, 2);
                    report.Deductions = Math.Round(deductions, 2);
                }
            }

            await _context.SaveChangesAsync();
        }

    }
    #endregion
}
