using Microsoft.AspNetCore.Identity;
using LeaveManagement.Models;
namespace LeaveManagement.SeedData
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Seed Roles
            string[] roles = { "Manager", "Employee" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed Manager User
            string managerEmail = "manager@bbox.in";
            string managerPassword = "Manager@123";

            var existingUser = await userManager.FindByEmailAsync(managerEmail);
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    EmailConfirmed = true,
                    FullName = "Rajat Khanna",
                    JoiningDate = DateTime.UtcNow,
                    BaseSalary = 100000,
                    Role = "Manager",
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, managerPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Manager");
                }
            }
        }
    }

}
