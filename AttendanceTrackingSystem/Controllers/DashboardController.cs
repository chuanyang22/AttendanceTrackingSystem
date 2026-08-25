using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? "User";

            var model = new DashboardViewModel
            {
                Role = userRole,
                FullName = userName,
                TotalStudents = await _context.Students.CountAsync(),
                TotalClasses = await _context.SchoolClasses.CountAsync(),
                TotalSessions = await _context.AttendanceSessions.CountAsync(),
                TotalUsers = await _context.Users.CountAsync()
            };

            if (userRole == "Student")
            {
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
                if (student != null)
                {
                    model.PresentCount = await _context.AttendanceRecords.CountAsync(r => r.StudentId == student.StudentId && r.Status == "Present");
                    model.AbsentCount = await _context.AttendanceRecords.CountAsync(r => r.StudentId == student.StudentId && r.Status == "Absent");
                    model.ExcusedCount = await _context.AttendanceRecords.CountAsync(r => r.StudentId == student.StudentId && r.Status == "Excused");

                    // Populate individual class stats
                    var enrollments = await _context.Enrollments
                        .Include(e => e.SchoolClass)
                        .Where(e => e.StudentId == student.StudentId)
                        .ToListAsync();

                    foreach (var e in enrollments)
                    {
                        var classRecords = await _context.AttendanceRecords
                            .Include(r => r.AttendanceSession)
                            .Where(r => r.StudentId == student.StudentId && r.AttendanceSession != null && r.AttendanceSession.ClassId == e.ClassId)
                            .ToListAsync();

                        model.ClassStats.Add(new StudentClassAttendanceStat
                        {
                            ClassName = e.SchoolClass?.ClassName ?? "Unknown",
                            Present = classRecords.Count(r => r.Status == "Present"),
                            Absent = classRecords.Count(r => r.Status == "Absent"),
                            Excused = classRecords.Count(r => r.Status == "Excused")
                        });
                    }
                }
            }
            else if (userRole == "Teacher")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int teacherUserId))
                {
                    var teacherClasses = await _context.SchoolClasses
                        .Where(c => c.TeacherId == teacherUserId)
                        .Include(c => c.Enrollments)
                        .ToListAsync();

                    var classIds = teacherClasses.Select(c => c.ClassId).ToList();

                    model.TotalClasses = teacherClasses.Count;
                    model.TotalStudents = teacherClasses.SelectMany(c => c.Enrollments).Select(e => e.StudentId).Distinct().Count();
                    model.TotalSessions = await _context.AttendanceSessions.CountAsync(s => classIds.Contains(s.ClassId));

                    model.PresentCount = await _context.AttendanceRecords.CountAsync(r => r.AttendanceSession != null && classIds.Contains(r.AttendanceSession.ClassId) && r.Status == "Present");
                    model.AbsentCount = await _context.AttendanceRecords.CountAsync(r => r.AttendanceSession != null && classIds.Contains(r.AttendanceSession.ClassId) && r.Status == "Absent");
                    model.ExcusedCount = await _context.AttendanceRecords.CountAsync(r => r.AttendanceSession != null && classIds.Contains(r.AttendanceSession.ClassId) && r.Status == "Excused");

                    // Load today's sessions for the teacher
                    var todaySessions = await _context.AttendanceSessions
                        .Where(s => classIds.Contains(s.ClassId) && s.SessionDate.Date == DateTime.Today)
                        .Include(s => s.SchoolClass)
                            .ThenInclude(c => c!.Enrollments)
                        .Include(s => s.AttendanceRecords)
                        .OrderBy(s => s.StartTime)
                        .ToListAsync();

                    model.TeacherTodaySessions = todaySessions.Select(s => new TeacherSessionItem
                    {
                        SessionId = s.SessionId,
                        ClassId = s.ClassId,
                        ClassName = s.SchoolClass?.ClassName ?? "Class",
                        SessionType = s.SessionType,
                        PinCode = s.QRCodeToken,
                        SessionDate = s.SessionDate,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        PresentCount = s.AttendanceRecords.Count(r => r.Status == "Present"),
                        TotalEnrolled = s.SchoolClass?.Enrollments?.Count ?? 0
                    }).ToList();
                }
            }
            else
            {
                model.PresentCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Present");
                model.AbsentCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Absent");
                model.ExcusedCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Excused");
            }

            int totalRecords = model.PresentCount + model.AbsentCount + model.ExcusedCount;
            model.OverallAttendanceRate = totalRecords > 0
                ? Math.Round(((double)(model.PresentCount) / totalRecords) * 100, 1)
                : 0.0;

            return View(model);
        }
    }
}