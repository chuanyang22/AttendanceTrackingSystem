using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class TimetableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimetableController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? weekDate)
        {
            // Default to current week's Monday
            DateTime date = weekDate ?? DateTime.Today;
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime weekStart = date.AddDays(-1 * diff).Date;
            DateTime weekEnd = weekStart.AddDays(6).Date;

            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value?.ToLower();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            List<SchoolClass> classes = new List<SchoolClass>();

            if (userRole == "Student")
            {
                var student = await _context.Students
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.SchoolClass)
                            .ThenInclude(c => c.Teacher)
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.SchoolClass)
                            .ThenInclude(c => c.AttendanceSessions)
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == userEmail);

                if (student != null)
                {
                    classes = student.Enrollments.Select(e => e.SchoolClass).ToList();
                }
            }
            else if (userRole == "Teacher")
            {
                var teacherId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                classes = await _context.SchoolClasses
                    .Include(c => c.Teacher)
                    .Include(c => c.AttendanceSessions)
                    .Where(c => c.TeacherId == teacherId)
                    .ToListAsync();
            }
            else // Admin
            {
                classes = await _context.SchoolClasses
                    .Include(c => c.Teacher)
                    .Include(c => c.AttendanceSessions)
                    .ToListAsync();
            }

            var holidays = await _context.PublicHolidays
                .Where(h => h.Date >= weekStart && h.Date <= weekEnd)
                .ToListAsync();

            var viewModel = new TimetableViewModel
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                Holidays = holidays
            };

            foreach (var c in classes)
            {
                // Parse regular schedule
                // e.g. "Wednesday 08:00-09:00"
                DayOfWeek? regularDay = null;
                TimeSpan? regularStart = null;
                TimeSpan? regularEnd = null;

                if (!string.IsNullOrEmpty(c.Schedule))
                {
                    var parts = c.Schedule.Split(' ');
                    if (parts.Length > 1 && Enum.TryParse<DayOfWeek>(parts[0], out var d))
                    {
                        regularDay = d;
                        var times = parts[1].Split('-');
                        if (times.Length == 2 && TimeSpan.TryParse(times[0], out var s) && TimeSpan.TryParse(times[1], out var e))
                        {
                            regularStart = s;
                            regularEnd = e;
                        }
                    }
                }

                // If regular schedule exists, create a slot for it
                if (regularDay.HasValue && regularStart.HasValue && regularEnd.HasValue)
                {
                    // Calculate the date for this regular day in the current week
                    int daysToAdd = ((int)regularDay.Value - (int)weekStart.DayOfWeek + 7) % 7;
                    DateTime regularClassDate = weekStart.AddDays(daysToAdd);

                    var slot = new TimetableSlot
                    {
                        ClassName = c.ClassName,
                        TeacherName = c.Teacher?.FullName ?? "",
                        Date = regularClassDate,
                        StartTime = regularStart.Value,
                        EndTime = regularEnd.Value,
                        Status = "Regular",
                        SessionType = "L" // default
                    };

                    // Check if it's a holiday
                    if (holidays.Any(h => h.Date.Date == regularClassDate.Date))
                    {
                        slot.Status = "Cancelled";
                    }
                    
                    viewModel.Slots.Add(slot);
                }

                // Add any replacement classes scheduled for this week
                var replacementSessions = c.AttendanceSessions
                    .Where(s => s.IsReplacement && s.SessionDate >= weekStart && s.SessionDate <= weekEnd)
                    .ToList();

                foreach (var rep in replacementSessions)
                {
                    viewModel.Slots.Add(new TimetableSlot
                    {
                        ClassName = c.ClassName,
                        TeacherName = c.Teacher?.FullName ?? "",
                        Date = rep.SessionDate,
                        StartTime = rep.StartTime ?? new TimeSpan(8, 0, 0),
                        EndTime = rep.EndTime ?? new TimeSpan(9, 0, 0),
                        Status = "Replacement",
                        SessionType = string.IsNullOrEmpty(rep.SessionType) ? "L" : rep.SessionType.Substring(0,1).ToUpper()
                    });
                }
            }

            return View(viewModel);
        }
    }
}


