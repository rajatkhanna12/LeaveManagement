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
        private readonly EmailService _emailService;

        public EmployeeController(LeaveDbContext context, UserManager<ApplicationUser> userManager, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
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
            try
            {

                var leaveTypeName = await _context.LeaveTypes
                   .Where(l => l.Id == model.LeaveTypeId)
                   .Select(l => l.Name)
                   .FirstOrDefaultAsync();

                model.LeaveType = new LeaveType { Name = leaveTypeName };
                string managerEmail = "rajatkhanna.netdeveloper@gmail.com"; // Can be fetched dynamically
                await SendLeaveRequestEmailAsync(user, model, managerEmail);
                Console.WriteLine($"📧 Leave request email sent to {managerEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send manager email: {ex.Message}");
            }
            TempData["Success"] = "Leave request submitted successfully!";
            return RedirectToAction("MyLeaves");
        }

        private async Task SendLeaveRequestEmailAsync(ApplicationUser user, LeaveRequest model, string managerEmail)
        {
            string subject = $"📝 New Leave Request from {user.FullName}";

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
                                            background-color: #007bff;
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
                                            color: #007bff;
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
                                            background-color: #f1f8ff;
                                            border-left: 4px solid #007bff;
                                            padding: 8px 12px;
                                            margin: 10px 0;
                                            border-radius: 4px;
                                        }}
                                    </style>
                                </head>
                                <body>
                                    <div class='email-container'>
                                        <div class='header'>New Leave Request Submitted</div>
                                        <div class='content'>
                                            <p>Hello <strong>Manager</strong>,</p>
                                            <p>A new leave request has been submitted by <strong>{user.FullName}</strong> (<a href='mailto:{user.Email}'>{user.Email}</a>).</p>

                                            <div class='highlight'>
                                                <p><strong>Leave Type:</strong> {model.LeaveType.Name}</p>
                                                <p><strong>Duration:</strong> {model.StartDate:dd MMM yyyy} - {model.EndDate:dd MMM yyyy}</p>
                                                <p><strong>Reason:</strong> {model.Reason}</p>
                                                <p><strong>Status:</strong> Pending</p>
                                            </div>

                                            <p>
                                                Please review and approve/reject the request in the Business Box panel.
                                            </p>
                                        </div>
                                        <div class='footer'>
                                            <p>This is an automated message from <strong>Business Box Leave Management System</strong>.</p>
                                        </div>
                                    </div>
                                </body>
                                </html>
                                ";

            await _emailService.SendEmailAsync(managerEmail, subject, body);
        }


        [HttpGet]
        public async Task<IActionResult> MyLeaveSummary()
        {
            await SetUserInfoAsync();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var today = DateTime.UtcNow;
            int currentMonth = today.Month;
            int currentYear = today.Year;

            var startOfYear = new DateTime(currentYear, 1, 1);
            var endOfCurrentMonth = new DateTime(currentYear, currentMonth, DateTime.DaysInMonth(currentYear, currentMonth));

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
            double totalUnpaid = 0;

            for (int month = 1; month <= currentMonth; month++)
            {
                double allowed = 1; // Monthly paid leave
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

            var model = new UserLeaveSummaryViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                LeaveTypeName = "Annual Leave",
                TotalAllocated = currentMonth,
                Used = Math.Round(totalPaidUsed, 1),
                Remaining = Math.Round(carryForward, 1)
            };

            return View(model);
        }




        [HttpGet]
        public async Task<IActionResult> MyLeaves()
        {
            await SetUserInfoAsync();
            var user = await _userManager.GetUserAsync(User);

            var leaves = await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.StartDate)
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
