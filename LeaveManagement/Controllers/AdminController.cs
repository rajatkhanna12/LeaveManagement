using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public IActionResult TodayAttendanceReport()
        {


            var today = DateTime.Today;

            var todayData = _context.tblAttendances
                .Where(a => a.CheckedInTime.Value.Date == today)
                .Select(a => new
                {
                    a.UserId,
                    a.CheckedInTime,
                    a.CheckedoutTime,
                    a.CheckedinImage,
                    a.CheckedoutImage,
                    UserName = _context.Users
                        .Where(u => u.Id == a.UserId.ToString())
                        .Select(u => u.FullName)
                        .FirstOrDefault()
                })
                .AsEnumerable()
                .GroupBy(a => new { a.UserId, a.UserName })
                .Select(g => new TodayAttendanceViewModel
                {
                    UserId = g.Key.UserId,
                    UserName = g.Key.UserName,
                    AttendanceHistory = g.Select(x => new AttendanceReportViewModel
                    {
                        CheckedInTime = (DateTime)x.CheckedInTime,
                        CheckedOutTime = x.CheckedoutTime,
                        CheckedInImage = x.CheckedinImage,
                        CheckedOutImage = x.CheckedoutImage,
                        WorkingHours = x.CheckedoutTime != null
    ? ((x.CheckedoutTime.Value - x.CheckedInTime.Value) - TimeSpan.FromMinutes(45)).TotalHours : (double?)null
                    }).ToList()
                })
                .ToList();

            return View(todayData);
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
            var request = await _context.LeaveRequests.FindAsync(model.Id);
            if (request == null) return NotFound();


            if (request.Status == LeaveStatus.Pending)
            {
                request.Status = model.Status;
                await _context.SaveChangesAsync();
                await CreateOrUpdateSalaryReportFromLeave(model.UserId, model.StartDate, model.EndDate, model.IsHalfDay);
            }

            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> RemainingLeaves()
        {
            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            int currentYear = today.Year;

            var startOfYear = new DateTime(currentYear, 1, 1);
            var endOfCurrentMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

            var users = await _userManager.GetUsersInRoleAsync("Employee");
            var summaries = new List<UserLeaveSummaryViewModel>();

            foreach (var user in users)
            {
                var approvedLeaves = await _context.LeaveRequests
                    .Where(l => l.UserId == user.Id &&
                                l.Status == LeaveStatus.Approved &&
                                l.EndDate >= startOfYear &&
                                l.StartDate <= endOfCurrentMonth)
                    .ToListAsync();

                // Step 1: Flatten all leave days
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

                // Step 2: Group by month
                var leavesByMonth = leaveDays
                    .GroupBy(ld => ld.date.Month)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToList()
                    );

                double carryForward = 0;
                double totalPaidUsed = 0;
                double totalUnpaid = 0;

                for (int month = 1; month <= currentMonth; month++)
                {
                    double allowed = 1; // Paid leaves allocated per month
                    double takenThisMonth = 0;

                    if (leavesByMonth.ContainsKey(month))
                    {
                        takenThisMonth = leavesByMonth[month].Sum(ld => ld.isHalfDay ? 0.5 : 1);
                    }

                    double available = allowed + carryForward;
                    double paidUsed = Math.Min(takenThisMonth, available);
                    double unpaid = Math.Max(0, takenThisMonth - available);

                    carryForward = Math.Max(0, available - paidUsed);
                    totalPaidUsed += paidUsed;
                    totalUnpaid += unpaid;
                }

                summaries.Add(new UserLeaveSummaryViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    LeaveTypeName = "Annual Leave",
                    TotalAllocated = currentMonth,
                    Used = Math.Round(totalPaidUsed, 1),

                    Remaining = Math.Round(carryForward, 1)
                });
            }

            return View(summaries);
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
