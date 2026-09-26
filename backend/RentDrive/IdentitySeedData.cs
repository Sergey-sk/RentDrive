using Microsoft.AspNetCore.Identity;
using RentDrive.db.models;
using Serilog;

namespace RentDrive
{
    public static class IdentitySeedData
    {
        private static readonly Serilog.ILogger logger = Log.ForContext(typeof(IdentitySeedData));

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roleNames = ["Customer", "Moderator", "Admin"];
            foreach (var roleName in roleNames)
            {
                var roleExists = await roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.Information("Создана роль {role}", roleName);
                }
            }

            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            string adminEmail = "admin@rentdrive.com";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createAdminResult = await userManager.CreateAsync(newAdmin, "admin123");

                if (createAdminResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                    logger.Information("Добавлен админ с id: {id}, email: {email}", newAdmin.Id, adminEmail);
                }
            }

        }
    }
}
