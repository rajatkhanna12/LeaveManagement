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

           
            bool alreadyExists = await _context.LeaveRequests.AnyAsync(l =>
                l.UserId == user.Id &&
                l.StartDate == model.StartDate &&
                l.EndDate == model.EndDate);

            if (alreadyExists)
            {
                ModelState.AddModelError("", "A leave request already exists for the selected start and end date.");
            }

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

            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfYear &&
                            l.StartDate <= endOfToday)
                .ToListAsync();

            double totalLeaveDays = 0;

            foreach (var leave in approvedLeaves)
            {
                var leaveStart = leave.StartDate < startOfYear ? startOfYear : leave.StartDate;
                var leaveEnd = leave.EndDate > endOfToday ? endOfToday : leave.EndDate;

                for (var dt = leaveStart; dt <= leaveEnd; dt = dt.AddDays(1))
                {
                    if (dt.Year == today.Year)
                    {
                        totalLeaveDays += leave.IsHalfDay ? 0.5 : 1;
                    }
                }
            }

            int allocatedPaidLeaves = currentMonth;
            int usedLeaves = (int)Math.Floor(totalLeaveDays);
            int remainingPaid = allocatedPaidLeaves - usedLeaves;

            var summary = new List<LeaveSummaryModel>
    {
        new LeaveSummaryModel
        {
            LeaveTypeName = "Monthly Leave",
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
            var today = DateTime.Now;
            var currentMonth = today.Month;
            var currentYear = today.Year;

            var previousDate = today.AddMonths(-1);
            var prevMonth = previousDate.Month;
            var prevYear = previousDate.Year;

            var prevUnpaid = await _context.SalaryReports
                .Include(r => r.User)
                .Where(r => r.UserId == user.Id.ToString() && !r.IsPaid &&
                            r.Month == prevMonth && r.Year == prevYear)
                .FirstOrDefaultAsync();

            if (prevUnpaid != null)
                return View(prevUnpaid);

            var currentUnpaid = await _context.SalaryReports
                .Include(r => r.User)
                .Where(r => r.UserId == user.Id.ToString() && !r.IsPaid &&
                            r.Month == currentMonth && r.Year == currentYear)
                .FirstOrDefaultAsync();

            if (currentUnpaid != null)
                return View(currentUnpaid);

            return View(null);
        }

    }
}
