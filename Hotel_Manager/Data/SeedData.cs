using Hotel_Manager.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Hotel_Manager.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Admin", "Receptionist", "Guest" };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Създаваме администратор
            var adminEmail = "admin@hotel.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Главен",
                    LastName = "Администратор",
                    Age = 35,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
                else
                {
                    // За debug – виж какви грешки връща Identity
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Не можа да се създаде admin: {errors}");
                }
            }
        }
    }
}