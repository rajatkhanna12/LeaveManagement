using Microsoft.AspNetCore.Mvc;
using LeaveManagement.Models;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Identity;
namespace LeaveManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager,
                                 UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
             var user = await _userManager.GetUserAsync(User);
            if(user == null)
            {
                return View();
            }
            else
            {
                if (user.Role == "Employee")
                {
                    return RedirectToAction("ApplyLeave", "Employee");
                }
                else if (user.Role == "Manager")
                {
                    return RedirectToAction("Index", "Admin");
                }
            }
           
            
         return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);

            if (result.Succeeded)
            {
                if (user.Role == "Employee")
                {
                    return RedirectToAction("ApplyLeave", "Employee");
                }
                else if (user.Role == "Manager")
                {
                    return RedirectToAction("Index", "Admin");
                }
                
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Login");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                await _signInManager.SignOutAsync();

                TempData["Success"] = "Password changed successfully. Please login again.";

                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }
        // forgot
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(
    ForgotPasswordViewModel model,
    [FromServices] EmailService emailService)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Do NOT reveal user existence
                TempData["Success"] = "If the email exists, a reset link has been sent.";
                return RedirectToAction("ForgotPassword");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { email = user.Email, token = token },
                protocol: Request.Scheme);

            string emailBody = $@"
        <p>Hello {user.UserName},</p>
        <p>You requested to reset your password.</p>
        <p>
            <a href='{resetLink}'
               style='padding:10px 15px;
               background:#696cff;
               color:white;
               text-decoration:none;
               border-radius:5px'>
               Reset Password
            </a>
        </p>
        <p>This link will expire automatically.</p>
        <br/>
        <p>– Business Box Team</p>";

            await emailService.SendEmailAsync(
                user.Email,
                "Reset your password",
                emailBody
            );

            TempData["Success"] = "Password reset link sent to your email.";
            return RedirectToAction("ForgotPassword");
        }

        //[HttpPost]
        //public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        //{
        //    if (!ModelState.IsValid) return View(model);

        //    var user = await _userManager.FindByEmailAsync(model.Email);
        //    if (user == null)
        //    {
        //        ModelState.AddModelError("", "No account found with this email.");
        //        return View(model);
        //    }

        //    // Reset password without needing the old password (Admin/forgot flow)
        //    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        //    var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

        //    if (result.Succeeded)
        //    {
        //       // TempData["Success"] = "Password has been reset successfully. You can now log in.";
        //        return RedirectToAction("Login");
        //    }

        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError("", error.Description);
        //    }
        //    return View(model);
        //}
        public IActionResult AccessDenied(string returnUrl) => RedirectToAction("Login");

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (email == null || token == null)
                return BadRequest("Invalid password reset link.");

            return View(new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction("Login");

            var result = await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Password reset successfully.";
                //return RedirectToAction("Login");
                if (user.Role == "Employee")
                {
                    return RedirectToAction("ApplyLeave", "Employee");
                }
                else if (user.Role == "Manager")
                {
                    return RedirectToAction("Index", "Admin");
                }
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

    }

}
