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
            return View(employees.AsEnumerable());
        }


        public async Task<IActionResult> Create()
        {
             await SetUserInfoAsync();
            return View();
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

        [HttpPost]
        public async Task<IActionResult> Create(ApplicationUser model, decimal baseSalary)
        {
            await SetUserInfoAsync();
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
            await CreateOrUpdateSalaryReportFromLeave(model.UserId, model.StartDate, model.EndDate, model.IsHalfDay);

            TempData["Success"] = "Leave request submitted successfully!";
            return RedirectToAction("AddLeave");
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
                .OrderByDescending(lr => lr.AppliedOn)
                .ToListAsync();

            return View( leaves); 
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

            return View( leaves);
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

        [HttpPost]
  
        public async Task<IActionResult> UpdateLeaveStatus(LeaveRequest model)
        {
            await SetUserInfoAsync();
            var request = await _context.LeaveRequests.FindAsync(model.Id);
            if (request == null) return NotFound();

           
            if (request.Status == LeaveStatus.Pending)
            {
                request.Status = model.Status;
                await _context.SaveChangesAsync();
               await  CreateOrUpdateSalaryReportFromLeave(model.UserId, model.StartDate, model.EndDate,model.IsHalfDay);
            }

            return RedirectToAction("Index");
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
                bool exists = await _context.SalaryReports
                    .AnyAsync(s => s.UserId == employee.Id && s.Month == currentMonth && s.Year == currentYear);

                if (exists)
                    continue;

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
                                l.StartDate.Month <= currentMonth )
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
            await SetUserInfoAsync();
            int leaveYear = leaveStartDate.Year;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive) return;

            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            var startOfYear = new DateTime(today.Year, 1, 1);
            var endOfToday = today;

            var allApprovedLeaves = await _context.LeaveRequests
                .Where(l => l.UserId == userId &&
                            l.Status == LeaveStatus.Approved &&
                            l.EndDate >= startOfYear &&
                            l.StartDate <= endOfToday)
                .ToListAsync();

            double totalLeaveDaysAlreadyTaken = 0;

            foreach (var leave in allApprovedLeaves)
            {
                // Exclude the current leave being processed
                if (leave.StartDate == leaveStartDate && leave.EndDate == leaveEndDate)
                    continue;

                var rangeStart = leave.StartDate < startOfYear ? startOfYear : leave.StartDate;
                var rangeEnd = leave.EndDate > endOfToday ? endOfToday : leave.EndDate;

                for (var dt = rangeStart; dt <= rangeEnd; dt = dt.AddDays(1))
                {
                    if (dt.Year == today.Year)
                        totalLeaveDaysAlreadyTaken += leave.IsHalfDay ? 0.5 : 1;
                }
            }

            DateTime current = leaveStartDate;

            while (current <= leaveEndDate)
            {
                var monthStart = new DateTime(current.Year, current.Month, 1);
                var monthEnd = new DateTime(current.Year, current.Month, DateTime.DaysInMonth(current.Year, current.Month));

                var leaveStart = current > monthStart ? current : monthStart;
                var leaveEnd = leaveEndDate < monthEnd ? leaveEndDate : monthEnd;

                int fullDays = (leaveEnd - leaveStart).Days + 1;
                double leaveDaysInThisMonth = isHalfDay ? fullDays * 0.5 : fullDays;

                int allowedPaidLeaveTillThisMonth = current.Month;
                double remainingPaidLeaves = allowedPaidLeaveTillThisMonth - totalLeaveDaysAlreadyTaken;

                double paid = Math.Min(leaveDaysInThisMonth, Math.Max(0, remainingPaidLeaves));
                double unpaid = leaveDaysInThisMonth - paid;

                totalLeaveDaysAlreadyTaken += leaveDaysInThisMonth;

                var report = await _context.SalaryReports
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.Year == current.Year && r.Month == current.Month);

                int totalDaysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                int joiningDay = (user.JoiningDate.Year == current.Year && user.JoiningDate.Month == current.Month)
                    ? user.JoiningDate.Day
                    : 1;
                int totalWorkingDays = totalDaysInMonth - (joiningDay - 1);

                decimal perDaySalary = user.BaseSalary / totalDaysInMonth;
                decimal deductions = (decimal)unpaid * perDaySalary;
                decimal proRatedBaseSalary = user.BaseSalary * totalWorkingDays / totalDaysInMonth;

                if (report == null)
                {
                    report = new SalaryReport
                    {
                        UserId = userId,
                        Year = current.Year,
                        Month = current.Month,
                        TotalWorkingDays = totalWorkingDays,
                        BaseSalary = proRatedBaseSalary,
                        LeaveTakenThisMonth = (float)leaveDaysInThisMonth,
                        Deductions = deductions,
                        Bonuses = 0,
                        IsPaid = false
                    };
                    _context.SalaryReports.Add(report);
                }
                else
                {
                    report.LeaveTakenThisMonth += (float)leaveDaysInThisMonth;
                    report.Deductions += deductions;
                    report.TotalWorkingDays = totalWorkingDays;
                    report.BaseSalary = proRatedBaseSalary;
                }

                current = leaveEnd.AddDays(1);
            }

            await _context.SaveChangesAsync();
        }



    }
    #endregion
}
