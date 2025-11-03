using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeaveManagement.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly LeaveDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AttendanceController(LeaveDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult Index()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                TempData["Message"] = "You must be logged in to check in!";
                return RedirectToAction("Login", "Account");
            }

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var attendanceToday = _context.tblAttendances
                .FirstOrDefault(a => a.UserId == userId && a.CheckedInTime.HasValue && a.CheckedInTime.Value.Date == today);

            ViewBag.HasCheckedIn = attendanceToday != null;
            ViewBag.HasCheckedOut = attendanceToday != null && attendanceToday.CheckedoutTime != null;

            var report = _context.tblAttendances
                .Where(a => a.UserId == userId && a.CreatedDate >= startOfMonth && a.CreatedDate <= endOfMonth)
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

        [HttpPost]
        public async Task<IActionResult> CheckIn(string imageData)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                TempData["Message"] = "You must be logged in to check in!";
                return RedirectToAction("Index");
            }

            var today = DateTime.Today;
            var alreadyCheckedIn = await _context.tblAttendances
                .AnyAsync(a => a.UserId == userId && a.CheckedInTime.HasValue && a.CheckedInTime.Value.Date == today);

            if (alreadyCheckedIn)
            {
                TempData["Message"] = "You have already checked in today!";
                return RedirectToAction("Index");
            }

            string imagePath = SaveBase64Image(imageData, "CheckedIn");

            var attendance = new TblAttendance
            {
                UserId = userId,
                CheckedInTime = DateTime.Now,
                CheckedinImage = imagePath,
                CreatedDate = DateTime.Now
            };

            _context.tblAttendances.Add(attendance);
            await _context.SaveChangesAsync();

            TempData["Message"] = " You’ve successfully checked in! Wishing you a productive and positive day ahead ";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(string imageData)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                TempData["Message"] = "You must be logged in to check out!";
                return RedirectToAction("Index");
            }

            var today = DateTime.Today;
            var attendance = await _context.tblAttendances
                .FirstOrDefaultAsync(a => a.UserId == userId && a.CheckedInTime.HasValue &&
                                          a.CheckedInTime.Value.Date == today && a.CheckedoutTime == null);

            if (attendance == null)
            {
                TempData["Message"] = "No check-in record found for today.";
                return RedirectToAction("Index");
            }

            string imagePath = SaveBase64Image(imageData, "CheckedOut");

            attendance.CheckedoutTime = DateTime.Now;
            attendance.CheckedoutImage = imagePath;

            await _context.SaveChangesAsync();

            TempData["Message"] = "Checked out! Hope you had a productive day. See you tomorrow!";
            return RedirectToAction("Index");
        }

        private string SaveBase64Image(string base64String, string type)
        {
            if (string.IsNullOrEmpty(base64String))
                return null;

            try
            {
                // 🧩 Handle "data:image/jpeg;base64," or "data:image/png;base64,"
                var base64Data = base64String;
                if (base64Data.Contains(","))
                    base64Data = base64Data.Split(',')[1];

                byte[] imageBytes = Convert.FromBase64String(base64Data);

                // 🗂️ Ensure upload directory exists
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "Attendance", type);
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // 🧾 Generate file name
                string fileName = $"{type}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 6)}.jpg";
                string filePath = Path.Combine(uploadsFolder, fileName);

                // 💾 Save image
                System.IO.File.WriteAllBytes(filePath, imageBytes);

                // 🌐 Return relative path (for use in <img src>)
                return $"/uploads/Attendance/{type}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving base64 image: {ex.Message}");
                return null;
            }
        }
    }
}
