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
            }

            if (!context.Users.Any(u => u.Email == "teacher@example.com"))
            {
                var teacherUser = new User
                {
                    FullName = "Default Teacher",
                    Email = "teacher@example.com",
                    PasswordHash = PasswordHelper.HashPassword("Teacher123!"),
                    Role = "Teacher",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(teacherUser);
            }

            if (!context.Users.Any(u => u.Email == "student@example.com"))
            {
                var studentUser = new User
                {
                    FullName = "Default Student",
                    Email = "student@example.com",
                    PasswordHash = PasswordHelper.HashPassword("Student123!"),
                    Role = "Student",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(studentUser);
            }

            context.SaveChanges();
        }
    }
}
