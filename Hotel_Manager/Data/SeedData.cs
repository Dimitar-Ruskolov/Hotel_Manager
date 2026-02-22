using Hotel_Manager.Models;
using Microsoft.AspNetCore.Identity;

namespace Hotel_Manager.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager =
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. ROLES

            string[] roles = { "Admin", "Receptionist", "Guest" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. ADMIN

            await CreateUserIfNotExists(
                userManager,
                email: "admin@hotel.com",
                password: "Admin123!",
                firstName: "Main",
                lastName: "Administrator",
                age: 35,
                role: "Admin"
            );

            // 3. RECEPTIONISTS

            await CreateUserIfNotExists(
                userManager,
                email: "reception1@hotel.com",
                password: "Reception123!",
                firstName: "Maria",
                lastName: "Ivanova",
                age: 28,
                role: "Receptionist"
            );

            await CreateUserIfNotExists(
                userManager,
                email: "reception2@hotel.com",
                password: "Reception123!",
                firstName: "Petar",
                lastName: "Dimitrov",
                age: 32,
                role: "Receptionist"
            );


            //  GUESTS


            await CreateUserIfNotExists(
                userManager,
                email: "guest1@hotel.com",
                password: "Guest123!",
                firstName: "Ivan",
                lastName: "Petrov",
                age: 24,
                role: "Guest"
            );

            await CreateUserIfNotExists(
                userManager,
                email: "guest2@hotel.com",
                password: "Guest123!",
                firstName: "Elena",
                lastName: "Georgieva",
                age: 29,
                role: "Guest"
            );

            await CreateUserIfNotExists(
                userManager,
                email: "guest3@hotel.com",
                password: "Guest123!",
                firstName: "Nikolay",
                lastName: "Stoyanov",
                age: 41,
                role: "Guest"
            );
        }


        private static async Task CreateUserIfNotExists(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string firstName,
            string lastName,
            int age,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user != null)
                return;

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Age = age,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}