using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Services;

namespace AttendanceTrackingSystem.Data
{
    public static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (!context.Users.Any(u => u.Email == "admin@example.com"))
            {
                var adminUser = new User
                {
                    FullName = "System Administrator",
                    Email = "admin@example.com",
                    PasswordHash = PasswordHelper.HashPassword("Admin123!"),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                context.Users.Add(adminUser);
                context.SaveChanges();
            }
        }
    }
}
