using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Services;

namespace AttendanceTrackingSystem.Data
{
    public static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (!context.Users.Any(u => u.Email == "teecy-wm25@student.tarc.edu.my"))
            {
                var adminUser = new User
                {
                    FullName = "System Administrator",
                    Email = "teecy-wm25@student.tarc.edu.my",
                    PasswordHash = PasswordHelper.HashPassword("Admin123!"),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(adminUser);
            }

            if (!context.Users.Any(u => u.Email == "tiesw-wm25@student.tarc.edu.my"))
            {
                var teacherUser = new User
                {
                    FullName = "Default Teacher",
                    Email = "tiesw-wm25@student.tarc.edu.my",
                    PasswordHash = PasswordHelper.HashPassword("Teacher123!"),
                    Role = "Teacher",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(teacherUser);
            }

            if (!context.Users.Any(u => u.Email == "shivendrat@student.tarc.edu.my"))
            {
                var studentUser = new User
                {
                    FullName = "Default Student",
                    Email = "shivendrat@student.tarc.edu.my",
                    PasswordHash = PasswordHelper.HashPassword("Student123!"),
                    Role = "Student",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(studentUser);
            }

            
            var studentUsers = context.Users.Where(u => u.Role == "Student").ToList();
            foreach (var su in studentUsers)
            {
                if (!context.Students.Any(s => s.Email == su.Email))
                {
                    context.Students.Add(new Student
                    {
                        Name = su.FullName,
                        Email = su.Email,
                        Phone = "0000000000",
                        Status = "Active"
                    });
                }
            }

            context.SaveChanges();
        }
    }
}


