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
        public AttendanceController(LeaveDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                TempData["Message"] = " You must be logged in to check in!";
                return RedirectToAction("Index");
            }

            var today = DateTime.Today;
            var attendance = _context.tblAttendances
                .FirstOrDefault(a => a.UserId == userId && a.CreatedDate.Date == today);

            ViewBag.HasCheckedIn = attendance != null;
            ViewBag.HasCheckedOut = attendance != null && attendance.CheckedoutTime != null;


            var report = _context.tblAttendances.Where(a => a.UserId == userId)
    .OrderByDescending(a => a.CheckedInTime)
    .Select(a => new AttendanceReportViewModel
    {

        CheckedInTime = a.CheckedInTime.Value, // force unwrap
        CheckedOutTime = a.CheckedoutTime,
        CheckedInImage = a.CheckedinImage,
        CheckedOutImage = a.CheckedoutImage,
        WorkingHours = a.CheckedoutTime != null
    ? ((a.CheckedoutTime.Value - a.CheckedInTime.Value) - TimeSpan.FromMinutes(45)).TotalHours: (double?)null
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
            string imagePath = SaveBase64Image(imageData, "CheckedIn");
            var today = DateTime.Today;
            var alreadyCheckedIn = await _context.tblAttendances
                .AnyAsync(a => a.UserId == userId && a.CheckedInTime == today);

            if (alreadyCheckedIn)
            {
                TempData["Message"] = "You have already checked in today!";
                return RedirectToAction("Index");
            }
            var attendance = new TblAttendance
            {
                UserId = userId,
                CheckedInTime = DateTime.Now,
                CheckedinImage = imagePath,
                CreatedDate = DateTime.Now
            };

            _context.tblAttendances.Add(attendance);
            await _context.SaveChangesAsync();

            TempData["Message"] = " You’ve successfully checked in! Wishing you a productive and positive day ahead 🚀";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(string imageData)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                TempData["Message"] = "You must be logged in to check in!";
                return RedirectToAction("Index");
            }
            string imagePath = SaveBase64Image(imageData, "CheckedOut");

            var attendance = _context.tblAttendances
                .FirstOrDefault(a => a.UserId == userId && a.CheckedoutTime == null);

            if (attendance != null)
            {
                attendance.CheckedoutTime = DateTime.Now;
                attendance.CheckedoutImage = imagePath;
                await _context.SaveChangesAsync();
                TempData["Message"] = " Checked out! Hope you had a productive day. See you tomorrow!";

            }
            else
            {
                TempData["Message"] = "No check-in record found for today.";
            }

            return RedirectToAction("Index");
        }

        private string SaveBase64Image(string base64String, string type)
        {
            if (string.IsNullOrEmpty(base64String))
                return null;


            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "Attendance", type);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + ".png";
            var filePath = Path.Combine(uploadsFolder, fileName);

            var base64Data = base64String.Replace("data:image/png;base64,", "");
            var bytes = Convert.FromBase64String(base64Data);
            System.IO.File.WriteAllBytes(filePath, bytes);


            return $"/uploads/Attendance/{type}/{fileName}";
        }

    }
}
