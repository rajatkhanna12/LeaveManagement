using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly LeaveDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
  
        public EmployeeController(LeaveDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        
        }

        [HttpGet]
        public async Task<IActionResult> ApplyLeave()
        {
            ViewBag.LeaveTypes = await _context.LeaveTypes.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLeave(LeaveRequest model)
        {
            var user = await _userManager.GetUserAsync(User);
        
            if (model.StartDate > model.EndDate)
            {
                ModelState.AddModelError("", "Start date must be before end date.");
            }
            if (model.StartDate <= DateTime.Today)
            {
                ModelState.AddModelError("StartDate", "Start date must be after today.");
            }
            if (model.EndDate <= DateTime.Today)
            {
                ModelState.AddModelError("EndDate", "End date must be after today.");
            }
            model.UserId = user.Id;
            model.Status = LeaveStatus.Pending;
            model.AppliedOn = DateTime.UtcNow;

            if (!ModelState.IsValid)
            {
                ViewBag.LeaveTypes = await _context.LeaveTypes.ToListAsync();
                return View(model);
            }

           

            _context.LeaveRequests.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Leave request submitted successfully!";
            return RedirectToAction("MyLeaves");
        }
        
        [HttpGet]
        public async Task<IActionResult> MyLeaveSummary()
        {
            var user = await _userManager.GetUserAsync(User);

            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            var startOfYear = new DateTime(today.Year, 1, 1);
            var endOfToday = today;

            // Get all approved leaves from start of year till now
            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfYear &&
                            l.StartDate <= endOfToday)
                .ToListAsync();



            double totalLeaveDays=0 ;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            foreach (var leave in approvedLeaves)
            {
                var leaveStart = leave.StartDate < startOfMonth ? startOfMonth : leave.StartDate;
                var leaveEnd = leave.EndDate > endOfMonth ? endOfMonth : leave.EndDate;

                double days = (leaveEnd - leaveStart).TotalDays + 1;

                totalLeaveDays += leave.IsHalfDay ? days * 0.5 : days;
            }


            var summary = new List<LeaveSummaryModel>
    {
        new LeaveSummaryModel
        {
            LeaveTypeName = "Casual Leave",
            TotalAllocated = currentMonth,
            Used = (int)totalLeaveDays,
            Remaining = (int)(currentMonth - totalLeaveDays)
        }
    };

            return View(summary);
        }



        [HttpGet]
        public async Task<IActionResult> MyLeaves()
        {
            var user = await _userManager.GetUserAsync(User);

            var leaves = await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.AppliedOn)
                .ToListAsync();

            return View(leaves);
        }
        [HttpGet]
        //public async Task<IActionResult> Estimated()
        //{
        //    var user = await _userManager.GetUserAsync(User);
        //    var now = DateTime.Now;

        //    var model = await _salaryService.GetEstimatedSalaryDetailsAsync(user.Id, now.Year, now.Month);
        //    return View(model); 
        //}
        [HttpGet]
        public async Task<IActionResult> SalaryDetail()
        {
            var user = await _userManager.GetUserAsync(User);
            var baseSalary = user.BaseSalary;

            var today = DateTime.UtcNow;
            var year = today.Year;
            var month = today.Month;
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            var startOfMonth = new DateTime(year, month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            
            int allowedLeavesTillNow = month;

           
            var allLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= new DateTime(year, 1, 1) &&
                            l.StartDate <= today)
                .ToListAsync();

           
            var currentMonthLeaves = allLeaves
                .Where(l => l.EndDate >= startOfMonth && l.StartDate <= endOfMonth)
                .ToList();

            double totalLeaveDays = 0;
            double thisMonthLeaveDays = 0;

            foreach (var leave in allLeaves)
            {
                var leaveStart = leave.StartDate < new DateTime(year, 1, 1) ? new DateTime(year, 1, 1) : leave.StartDate;
                var leaveEnd = leave.EndDate > today ? today : leave.EndDate;
                double days = (leaveEnd - leaveStart).TotalDays + 1;

                totalLeaveDays += leave.IsHalfDay ? days * 0.5 : days;
            }

            // Count current month leave days
            foreach (var leave in currentMonthLeaves)
            {
                var leaveStart = leave.StartDate < startOfMonth ? startOfMonth : leave.StartDate;
                var leaveEnd = leave.EndDate > endOfMonth ? endOfMonth : leave.EndDate;
                double days = (leaveEnd - leaveStart).TotalDays + 1;

                thisMonthLeaveDays += leave.IsHalfDay ? days * 0.5 : days;
            }
            
            // Logic for deductions
            double extraLeaveDaysTotal = totalLeaveDays - allowedLeavesTillNow;
            if (extraLeaveDaysTotal < 0) extraLeaveDaysTotal = 0;

            double allowedLeavesThisMonth = 1; // Assuming 1 per month
            double extraLeaveThisMonth = thisMonthLeaveDays - allowedLeavesThisMonth;
            if (extraLeaveThisMonth < 0) extraLeaveThisMonth = 0;

            double paidLeavesThisMonth = thisMonthLeaveDays - extraLeaveThisMonth;

            decimal perDaySalary = baseSalary / totalDaysInMonth;
            decimal estimatedSalary = baseSalary - (perDaySalary * (decimal)extraLeaveDaysTotal);

            var result = new SalaryModel
            {
                BaseSalary = baseSalary,
                TotalLeaves = totalLeaveDays,
                LeavesTakenThisMonth = thisMonthLeaveDays,
                PaidLeavesThisMonth = paidLeavesThisMonth,
                ExtraLeaveDays = extraLeaveDaysTotal,
                ExtraLeaveDaysThisMonth = extraLeaveThisMonth,
                EstimatedSalary = estimatedSalary,
                PerDaySalary = perDaySalary,
                TotalDaysInMonth = totalDaysInMonth
            };

            return View(result);
        }

        //[HttpGet]
        //public async Task<IActionResult> LeaveDetails(int id)
        //{
        //    var user = await _userManager.GetUserAsync(User);

        //    var leave = await _context.LeaveRequests
        //        .Include(l => l.LeaveType)
        //        .FirstOrDefaultAsync(l => l.Id == id && l.UserId == user.Id);

        //    if (leave == null)
        //        return NotFound();

        //    return View(leave);
        //}
    }
}
