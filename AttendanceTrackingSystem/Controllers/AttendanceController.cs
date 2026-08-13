using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;
using AttendanceTrackingSystem.Services;

namespace AttendanceTrackingSystem.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AttendanceController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: Attendance Sessions List
        public async Task<IActionResult> Index(int? classId)
        {
            var query = _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .Include(s => s.AttendanceRecords)
                .AsQueryable();

            if (classId.HasValue)
            {
                query = query.Where(s => s.ClassId == classId.Value);
            }

            ViewData["ClassId"] = new SelectList(_context.SchoolClasses.OrderBy(c => c.ClassName), "ClassId", "ClassName", classId);
            var sessions = await query.OrderByDescending(s => s.SessionDate).ToListAsync();
            return View(sessions);
        }

        // GET: Create Attendance Session
        public IActionResult CreateSession()
        {
            ViewData["ClassId"] = new SelectList(_context.SchoolClasses.OrderBy(c => c.ClassName), "ClassId", "ClassName");
            return View(new AttendanceSession { SessionDate = DateTime.Today });
        }

        // POST: Create Attendance Session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession([Bind("ClassId,SessionDate,Topic")] AttendanceSession session)
        {

            if (ModelState.IsValid)
            {
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
                        Status = "Present" // Default to Present
                    });
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Mark), new { id = session.SessionId });
            }

            ViewData["ClassId"] = new SelectList(_context.SchoolClasses.OrderBy(c => c.ClassName), "ClassId", "ClassName", session.ClassId);
            return View(session);
        }

        // GET: Mark Attendance Grid
        public async Task<IActionResult> Mark(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .Include(s => s.AttendanceRecords)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return NotFound();

            var viewModel = new MarkAttendanceViewModel
            {
                SessionId = session.SessionId,
                ClassName = session.SchoolClass?.ClassName ?? "N/A",
                TeacherName = session.SchoolClass?.TeacherName ?? "N/A",
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

            foreach (var item in model.Students)
            {
                var record = session.AttendanceRecords.FirstOrDefault(r => r.StudentId == item.StudentId);
                if (record != null)
                {
                    bool wasNotAbsent = record.Status != "Absent";
                    record.Status = item.Status;
                    record.Remarks = item.Remarks;
                    record.MarkedAt = DateTime.Now;

                    // Additional Feature: Email Notification on Absence
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
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Attendance updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // Additional Feature: Display QR Code for Attendance Check-in
        public async Task<IActionResult> QRCode(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return NotFound();
            return View(session);
        }

        // Additional Feature: Quick Check-In via QR Code URL
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