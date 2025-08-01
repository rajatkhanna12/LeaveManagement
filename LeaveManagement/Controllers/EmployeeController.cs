using iTextSharp.text.pdf;
using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using SelectPdf;
using PdfDocument = SelectPdf.PdfDocument;

namespace LeaveManagement.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly LeaveDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        public EmployeeController(LeaveDbContext context, UserManager<ApplicationUser> userManager, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider)
        {
            _context = context;
            _userManager = userManager;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;

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


        [HttpGet]
        public async Task<IActionResult> ApplyLeave()
        {
            await SetUserInfoAsync();
            ViewBag.LeaveTypes = await _context.LeaveTypes.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyLeave(LeaveRequest model)
        {
            await SetUserInfoAsync();
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
            await SetUserInfoAsync();
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
            await SetUserInfoAsync();
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
            await SetUserInfoAsync();
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
        [HttpGet]
        public async Task<IActionResult> DownloadSalarySlipPdf(int id)
        {
            var report = await _context.SalaryReports
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            // ✅ Render only data section
            string htmlContent = await RenderViewToStringAsync("_SalarySlipPartial", report);

            // ✅ Convert partial HTML to PDF
            HtmlToPdf converter = new HtmlToPdf
            {
                Options =
        {
            PdfPageSize = PdfPageSize.A4,
            PdfPageOrientation = PdfPageOrientation.Portrait,
            WebPageWidth = 800
        }
            };

            PdfDocument doc = converter.ConvertHtmlString(htmlContent);
            byte[] pdf = doc.Save();
            doc.Close();

            return File(pdf, "application/pdf", $"SalarySlip_{report.Month}_{report.Year}.pdf");
        }


        // ✅ Helper method to render Razor view as string
        private async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model)
        {
            var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor, ModelState);

            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(actionContext, viewName, false);
                if (!viewResult.Success)
                    throw new FileNotFoundException($"View '{viewName}' not found.");

                var viewDictionary = new ViewDataDictionary<TModel>(
                    MetadataProvider, ModelState)
                {
                    Model = model
                };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.GetStringBuilder().ToString();
            }
        }
    }
}
