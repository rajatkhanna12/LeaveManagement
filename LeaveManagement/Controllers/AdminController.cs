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
        public async Task<IActionResult> LeaveRequests()
        {
            var leaveRequests = await _context.LeaveRequests
                .Include(lr => lr.User)
                .Include(lr => lr.LeaveType)
                .ToListAsync();

            return View(leaveRequests);
        }
        [HttpGet]
        public async Task<IActionResult> LeaveByUser(string userId)
        {
            var leaveRequests = await _context.LeaveRequests
                .Where(lr => lr.UserId == userId)
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

            request.Status = model.Status;
            await _context.SaveChangesAsync();

            return RedirectToAction("LeaveRequests");
        }

    }
}
