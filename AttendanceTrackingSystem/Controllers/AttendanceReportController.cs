using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class AttendanceReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Student Individual Attendance History & Percentage
        [HttpGet]
        public async Task<IActionResult> StudentHistory(int? studentId)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var isStudent = User.IsInRole("Student");

            // Auto-detect matching student profile if logged in as Student
            if (isStudent && !string.IsNullOrEmpty(userEmail))
            {
                var studentProfile = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email.ToLower() == userEmail.ToLower());

                if (studentProfile != null)
                {
                    studentId = studentProfile.StudentId;
                }
            }

            IQueryable<Student> studentsQuery = _context.Students.AsQueryable();
            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    studentsQuery = _context.Enrollments
                        .Include(e => e.SchoolClass)
                        .Include(e => e.Student)
                        .Where(e => e.SchoolClass != null && e.SchoolClass.TeacherId == userId && e.Student != null)
                        .Select(e => e.Student!)
                        .Distinct();
                }
            }

            if (!studentId.HasValue)
            {
                ViewData["StudentId"] = new SelectList(await studentsQuery.OrderBy(s => s.Name).ToListAsync(), "StudentId", "Name");
                return View(null);
            }

            var student = await _context.Students
                .Include(s => s.AttendanceRecords)
                    .ThenInclude(r => r.AttendanceSession)
                        .ThenInclude(s => s!.SchoolClass)
                .FirstOrDefaultAsync(s => s.StudentId == studentId.Value);

            if (student == null)
            {
                ViewData["StudentId"] = new SelectList(await studentsQuery.OrderBy(s => s.Name).ToListAsync(), "StudentId", "Name", studentId);
                return View(null);
            }

            var records = student.AttendanceRecords
                .Where(r => r.AttendanceSession != null)
                .Select(r => new AttendanceRecordDetail
                {
                    Date = r.AttendanceSession?.SessionDate ?? DateTime.MinValue,
                    ClassName = r.AttendanceSession?.SchoolClass?.ClassName ?? "N/A",
                    Status = r.Status,
                    Remarks = r.Remarks
                })
                .OrderByDescending(r => r.Date)
                .ToList();

            var summary = new StudentAttendanceSummaryViewModel
            {
                StudentId = student.StudentId,
                StudentName = student.Name,
                StudentEmail = student.Email,
                TotalSessions = records.Count,
                PresentCount = records.Count(r => r.Status == "Present"),
                AbsentCount = records.Count(r => r.Status == "Absent"),
                ExcusedCount = records.Count(r => r.Status == "Excused"),
                Records = records
            };

            ViewData["StudentId"] = new SelectList(await studentsQuery.OrderBy(s => s.Name).ToListAsync(), "StudentId", "Name", studentId);
            return View(summary);
        }

        // Class Monthly Report
        [HttpGet]
        public async Task<IActionResult> ClassReport(int? classId, int? month, int? year)
        {
            int selectedMonth = month ?? DateTime.Now.Month;
            int selectedYear = year ?? DateTime.Now.Year;

            IQueryable<SchoolClass> classesQuery = _context.SchoolClasses.AsQueryable();
            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    classesQuery = classesQuery.Where(c => c.TeacherId == userId);
                }
            }

            ViewData["ClassId"] = new SelectList(await classesQuery.OrderBy(c => c.ClassName).ToListAsync(), "ClassId", "ClassName", classId);
            ViewData["SelectedMonth"] = selectedMonth;
            ViewData["SelectedYear"] = selectedYear;

            if (!classId.HasValue) return View(null);

            var schoolClass = await _context.SchoolClasses.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.ClassId == classId.Value);
            if (schoolClass == null) return NotFound();

            var sessions = await _context.AttendanceSessions
                .Where(s => s.ClassId == classId.Value && s.SessionDate.Month == selectedMonth && s.SessionDate.Year == selectedYear)
                .Include(s => s.AttendanceRecords)
                    .ThenInclude(r => r.Student)
                .ToListAsync();

            var enrolledStudents = await _context.Enrollments
                .Where(e => e.ClassId == classId.Value)
                .Include(e => e.Student)
                .Select(e => e.Student!)
                .ToListAsync();

            var studentSummaries = new List<StudentAttendanceSummaryViewModel>();

            foreach (var student in enrolledStudents)
            {
                var studentRecords = sessions.SelectMany(s => s.AttendanceRecords)
                    .Where(r => r.StudentId == student.StudentId)
                    .ToList();

                studentSummaries.Add(new StudentAttendanceSummaryViewModel
                {
                    StudentId = student.StudentId,
                    StudentName = student.Name,
                    StudentEmail = student.Email,
                    TotalSessions = sessions.Count,
                    PresentCount = studentRecords.Count(r => r.Status == "Present"),
                    AbsentCount = studentRecords.Count(r => r.Status == "Absent"),
                    ExcusedCount = studentRecords.Count(r => r.Status == "Excused")
                });
            }

            var reportModel = new ClassAttendanceReportViewModel
            {
                ClassId = schoolClass.ClassId,
                ClassName = schoolClass.ClassName,
                TeacherName = schoolClass.Teacher?.FullName ?? "Unknown",
                Month = selectedMonth,
                Year = selectedYear,
                TotalSessions = sessions.Count,
                StudentSummaries = studentSummaries
            };

            return View(reportModel);
        }

        // Additional Feature: PDF Printable Attendance Report
        [HttpGet]
        public async Task<IActionResult> PrintReport(int classId, int month, int year)
        {
            var reportModel = await ClassReportDataAsync(classId, month, year);
            if (reportModel == null) return NotFound();
            return View(reportModel);
        }

                private async Task<ClassAttendanceReportViewModel?> ClassReportDataAsync(int classId, int month, int year)
        {
            var schoolClass = await _context.SchoolClasses.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.ClassId == classId);
            if (schoolClass == null) return null;

            var sessions = await _context.AttendanceSessions
                .Where(s => s.ClassId == classId && s.SessionDate.Month == month && s.SessionDate.Year == year)
                .Include(s => s.AttendanceRecords)
                .ToListAsync();

            var enrolledStudents = await _context.Enrollments
                .Where(e => e.ClassId == classId)
                .Include(e => e.Student)
                .Select(e => e.Student!)
                .ToListAsync();

            var studentSummaries = enrolledStudents.Select(student =>
            {
                var records = sessions.SelectMany(s => s.AttendanceRecords).Where(r => r.StudentId == student.StudentId).ToList();
                return new StudentAttendanceSummaryViewModel
                {
                    StudentId = student.StudentId,
                    StudentName = student.Name,
                    StudentEmail = student.Email,
                    TotalSessions = sessions.Count,
                    PresentCount = records.Count(r => r.Status == "Present"),
                    AbsentCount = records.Count(r => r.Status == "Absent"),
                    ExcusedCount = records.Count(r => r.Status == "Excused")
                };
            }).ToList();

            return new ClassAttendanceReportViewModel
            {
                ClassId = schoolClass.ClassId,
                ClassName = schoolClass.ClassName,
                TeacherName = schoolClass.Teacher?.FullName ?? "Unknown",
                Month = month,
                Year = year,
                TotalSessions = sessions.Count,
                StudentSummaries = studentSummaries
            };
        }
    }
}


