using LeaveManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Controllers
{
    [Authorize(Roles = "Manager")]
    public class AdminController : Controller
    {
        private readonly LeaveDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(LeaveDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public  async Task<IActionResult> Index()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            return View(employees.AsEnumerable());
        }

     
        public IActionResult Create()
        {
            return View();
        }



        [HttpGet]
        public async Task<IActionResult> SalaryDetail(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var baseSalary = user.BaseSalary;

            var today = DateTime.UtcNow;
            var year = today.Year;
            var month = today.Month;
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            var startOfMonth = new DateTime(year, month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            // Total allowed paid leaves till now (1 per month)
            int allowedLeavesTillNow = month;

            // Get all approved leaves from Jan 1 to today
            var allLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == user.Id &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= new DateTime(year, 1, 1) &&
                            l.StartDate <= today)
                .ToListAsync();

            // Get only current month's approved leaves
            var currentMonthLeaves = allLeaves
                .Where(l => l.EndDate >= startOfMonth && l.StartDate <= endOfMonth)
                .ToList();

            double totalLeaveDays = 0;
            double thisMonthLeaveDays = 0;

            // Count full year leave days
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

        [HttpPost]
        public async Task<IActionResult> Create(ApplicationUser model, decimal baseSalary)
        {
            if (ModelState.IsValid)
            {
                model.BaseSalary = baseSalary;
                model.UserName = model.Email;
                model.EmailConfirmed = true;
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

        [HttpGet]
       
        public async Task<IActionResult> AddLeave()
        {
            var employeeRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Employee");

            // Fetch all users in the 'Employee' role
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
            model.Status = LeaveStatus.Approved;
            model.AppliedOn = DateTime.UtcNow;

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

            TempData["Success"] = "Leave request submitted successfully!";
            return RedirectToAction("AddLeave");
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        
        [HttpPost]
        public async Task<IActionResult> Edit(ApplicationUser model, decimal baseSalary)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.JoiningDate = model.JoiningDate;
                user.BaseSalary = baseSalary;
                user.Role = model.Role;
                user.IsActive = model.IsActive;

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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> ApprovedLeaves()
        {
            var leaves = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveStatus.Approved)
                .OrderByDescending(lr => lr.AppliedOn)
                .ToListAsync();

            return View( leaves); 
        }

        [HttpGet]
        public async Task<IActionResult> RejectedLeaves()
        {
            var leaves = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveStatus.Rejected)
                .OrderByDescending(lr => lr.AppliedOn)
                .ToListAsync();

            return View( leaves);
        }

        [HttpGet]
        public async Task<IActionResult> PendingLeaves()
        {
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
            ViewBag.UserId = id;    
            var leaveRequests = await _context.LeaveRequests
                .Where(lr => lr.UserId == id)
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .ToListAsync();

            return View(leaveRequests);
        }
        [HttpGet]
        public async Task<IActionResult> UpdateLeaveStatus(int id)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.User)
                .Include(r => r.LeaveType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            return View(request);
        }

        [HttpPost]
  
        public async Task<IActionResult> UpdateLeaveStatus(LeaveRequest model)
        {
            var request = await _context.LeaveRequests.FindAsync(model.Id);
            if (request == null) return NotFound();

            // Only update if the current status is Pending
            if (request.Status == LeaveStatus.Pending)
            {
                request.Status = model.Status;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

    }
}
