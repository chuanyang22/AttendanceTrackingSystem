using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;
using AttendanceTrackingSystem.Services;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AttendanceController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private IQueryable<SchoolClass> GetAllowedClasses()
        {
            var classes = _context.SchoolClasses.AsQueryable();
            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    classes = classes.Where(c => c.TeacherId == userId);
                }
            }
            return classes;
        }

        // GET: Attendance Sessions List
        [HttpGet]
        public async Task<IActionResult> Index(int? classId)
        {
            var query = _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .Include(s => s.AttendanceRecords)
                .AsQueryable();

            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    query = query.Where(s => s.SchoolClass != null && s.SchoolClass.TeacherId == userId);
                }
            }
            else if (User.IsInRole("Student"))
            {
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
                query = query.Where(s => s.SchoolClass != null && s.SchoolClass.Enrollments.Any(e => e.Student.Email.ToLower() == email));
            }

            if (classId.HasValue)
            {
                query = query.Where(s => s.ClassId == classId.Value);
            }

            var allowedClasses = await GetAllowedClasses().OrderBy(c => c.ClassName).ToListAsync();
            ViewData["ClassId"] = new SelectList(allowedClasses, "ClassId", "ClassName", classId);
            
            var sessions = await query.OrderByDescending(s => s.SessionDate).ToListAsync();
            return View(sessions);
        }

        // GET: Create Attendance Session
        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet]
        public async Task<IActionResult> CreateSession()
        {
            var allowedClasses = await GetAllowedClasses().OrderBy(c => c.ClassName).ToListAsync();
            ViewData["ClassId"] = new SelectList(allowedClasses, "ClassId", "ClassName");
            return View(new AttendanceSession { SessionDate = DateTime.Today });
        }

        // POST: Create Attendance Session
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> CreateSession([Bind("ClassId,SessionDate,Topic")] AttendanceSession session)
        {
            if (ModelState.IsValid)
            {
                // Verify the user has access to this class
                var allowedClasses = await GetAllowedClasses().Select(c => c.ClassId).ToListAsync();
                if (!allowedClasses.Contains(session.ClassId))
                {
                    return Forbid();
                }

                _context.AttendanceSessions.Add(session);
                await _context.SaveChangesAsync();

                // Auto-create initial records for all enrolled students
                var enrolledStudentIds = await _context.Enrollments
                    .Where(e => e.ClassId == session.ClassId)
                    .Select(e => e.StudentId)
                    .ToListAsync();

                foreach (var studentId in enrolledStudentIds)
                {
                    _context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        SessionId = session.SessionId,
                        StudentId = studentId,
                        Status = "Present"
                    });
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var classes = await GetAllowedClasses().OrderBy(c => c.ClassName).ToListAsync();
            ViewData["ClassId"] = new SelectList(classes, "ClassId", "ClassName", session.ClassId);
            return View(session);
        }

        // GET: Mark Attendance Grid / Student Self-Check-in
        [HttpGet]
        public async Task<IActionResult> Mark(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                    .ThenInclude(c => c.Teacher)
                .Include(s => s.AttendanceRecords)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return NotFound();

            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Student";
            var isStudent = User.IsInRole("Student");

            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId) && session.SchoolClass?.TeacherId != userId)
                {
                    return Forbid();
                }
            }

            // If a Student accesses Mark Attendance, guarantee they exist and are present in this session
            if (isStudent && !string.IsNullOrEmpty(userEmail))
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == userEmail.ToLower());
                if (student == null)
                {
                    student = new Student
                    {
                        Name = userName,
                        Email = userEmail
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                }

                // Ensure enrolled in this class
                var enrolled = await _context.Enrollments.AnyAsync(e => e.ClassId == session.ClassId && e.StudentId == student.StudentId);
                if (!enrolled)
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        ClassId = session.ClassId,
                        StudentId = student.StudentId,
                        EnrollDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }

                // Ensure attendance record exists for this session
                var record = session.AttendanceRecords.FirstOrDefault(r => r.StudentId == student.StudentId);
                if (record == null)
                {
                    record = new AttendanceRecord
                    {
                        SessionId = session.SessionId,
                        StudentId = student.StudentId,
                        Status = "Present",
                        MarkedAt = DateTime.Now
                    };
                    _context.AttendanceRecords.Add(record);
                    await _context.SaveChangesAsync();
                }

                // Build single-student view model for the current student
                var studentViewModel = new MarkAttendanceViewModel
                {
                    SessionId = session.SessionId,
                    ClassName = session.SchoolClass?.ClassName ?? "N/A",
                    TeacherName = session.SchoolClass?.Teacher?.FullName ?? "N/A",
                    SessionDate = session.SessionDate,
                    Topic = session.Topic,
                    Students = new List<StudentAttendanceItem>
                    {
                        new StudentAttendanceItem
                        {
                            StudentId = student.StudentId,
                            StudentName = student.Name,
                            StudentEmail = student.Email,
                            Status = record.Status,
                            Remarks = record.Remarks
                        }
                    }
                };

                return View(studentViewModel);
            }

            // For Admin/Teacher: show all enrolled students
            var viewModel = new MarkAttendanceViewModel
            {
                SessionId = session.SessionId,
                ClassName = session.SchoolClass?.ClassName ?? "N/A",
                TeacherName = session.SchoolClass?.Teacher?.FullName ?? "N/A",
                SessionDate = session.SessionDate,
                Topic = session.Topic,
                Students = session.AttendanceRecords.Select(r => new StudentAttendanceItem
                {
                    StudentId = r.StudentId,
                    StudentName = r.Student?.Name ?? "Unknown",
                    StudentEmail = r.Student?.Email ?? "",
                    Status = r.Status,
                    Remarks = r.Remarks
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: Save Marked Attendance & Trigger Email Notification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mark(MarkAttendanceViewModel model)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .Include(s => s.AttendanceRecords)
                .FirstOrDefaultAsync(s => s.SessionId == model.SessionId);

            if (session == null) return NotFound();

            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId) && session.SchoolClass?.TeacherId != userId)
                {
                    return Forbid();
                }
            }

            var isStudent = User.IsInRole("Student");

            foreach (var item in model.Students)
            {
                var record = session.AttendanceRecords.FirstOrDefault(r => r.StudentId == item.StudentId);
                if (record != null)
                {
                    bool wasNotAbsent = record.Status != "Absent";
                    record.Status = item.Status ?? "Present";
                    record.Remarks = item.Remarks;
                    record.MarkedAt = DateTime.Now;

                    if (item.Status == "Absent" && wasNotAbsent && !string.IsNullOrEmpty(item.StudentEmail))
                    {
                        await _emailService.SendAbsenceNotificationAsync(
                            item.StudentEmail,
                            item.StudentName,
                            session.SchoolClass?.ClassName ?? "Class",
                            session.SessionDate
                        );
                    }
                }
                else
                {
                    _context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        SessionId = session.SessionId,
                        StudentId = item.StudentId,
                        Status = item.Status ?? "Present",
                        Remarks = item.Remarks,
                        MarkedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Attendance recorded successfully!";

            if (isStudent)
            {
                var currentStudentId = model.Students.FirstOrDefault()?.StudentId;
                return RedirectToAction("StudentHistory", "AttendanceReport", new { studentId = currentStudentId });
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Display QR Code for Attendance Check-in
        [HttpGet]
        public async Task<IActionResult> QRCode(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return NotFound();
            
            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId) && session.SchoolClass?.TeacherId != userId)
                {
                    return Forbid();
                }
            }
            
            return View(session);
        }

        // GET: Quick Check-In via QR Code URL
        [HttpGet]
        public async Task<IActionResult> QuickCheckIn(string token, int studentId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .FirstOrDefaultAsync(s => s.QRCodeToken == token);

            if (session == null) return NotFound("Invalid QR Code session token.");

            var record = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.SessionId == session.SessionId && r.StudentId == studentId);

            if (record != null)
            {
                record.Status = "Present";
                record.MarkedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "You have checked in successfully via QR Code!";
            }

            return RedirectToAction("StudentHistory", "AttendanceReport", new { studentId = studentId });
        }
    }
}
