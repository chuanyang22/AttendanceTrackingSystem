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

            // Aggregate Attendance Status Counts for Charts
            model.PresentCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Present");
            model.AbsentCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Absent");
            model.LateCount = await _context.AttendanceRecords.CountAsync(r => r.Status == "Late");

            int totalRecords = model.PresentCount + model.AbsentCount + model.LateCount;
            model.OverallAttendanceRate = totalRecords > 0
                ? Math.Round(((double)(model.PresentCount + model.LateCount) / totalRecords) * 100, 1)
                : 0.0;

            return View(model);
        }
    }
}