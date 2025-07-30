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

           
            var leaveTypes = await _context.LeaveTypes.ToListAsync();

            
            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id && l.Status == LeaveStatus.Approved)
                .ToListAsync();

           
            var summary = leaveTypes.Select(type =>
            {
                var usedDays = approvedLeaves
                    .Where(l => l.LeaveTypeId == type.Id)
                    .Sum(l => (l.EndDate - l.StartDate).Days + 1); 

                return new LeaveSummaryModel
                {
                    LeaveTypeName = type.Name,
                    TotalAllocated = type.TotalAllowed,
                    Used = usedDays,
                    Remaining = type.TotalAllowed - usedDays
                };
            }).ToList();

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
        public async Task<IActionResult> SalaryDetail()
        {
            var user = await _userManager.GetUserAsync(User);
            var baseSalary = user.BaseSalary;

            var today = DateTime.UtcNow;
            var year = today.Year;
            var month = today.Month;
            var startOfMonth = new DateTime(year, month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // Get all approved leaves overlapping with current month
            var leaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfMonth &&
                            l.StartDate <= endOfMonth)
                .ToListAsync();

            double totalLeaveDays = 0;

            foreach (var leave in leaves)
            {
                if (leave.IsHalfDay)
                {
                    totalLeaveDays += 0.5;
                }
                else
                {
                    var leaveStart = leave.StartDate < startOfMonth ? startOfMonth : leave.StartDate;
                    var leaveEnd = leave.EndDate > endOfMonth ? endOfMonth : leave.EndDate;
                    totalLeaveDays += (leaveEnd - leaveStart).TotalDays + 1;
                }
            }

            decimal perDaySalary = baseSalary / totalDaysInMonth;
            decimal estimatedSalary = baseSalary - (perDaySalary * (decimal)totalLeaveDays);

            var result = new SalaryModel
            {
                BaseSalary = baseSalary,
                TotalLeaves = totalLeaveDays,
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
