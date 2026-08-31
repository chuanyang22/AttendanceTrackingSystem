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
                query = query.Where(predicate: s => s.SchoolClass != null && s.SchoolClass.Enrollments.Any(e => e.Student != null && e.Student.Email != null && e.Student.Email.ToLower() == email));
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
        public async Task<IActionResult> CreateSession([Bind("ClassId,SessionDate,SessionType,Topic,IsReplacement,StartTime,EndTime")] AttendanceSession session)
        {
            if (ModelState.IsValid)
            {
                // Verify the user has access to this class
                var allowedClasses = await GetAllowedClasses().Select(c => c.ClassId).ToListAsync();
                if (!allowedClasses.Contains(session.ClassId))
                {
                    return Forbid();
                }

                // Populate regular session times from Schedule if not provided
                if (!session.IsReplacement && (!session.StartTime.HasValue || !session.EndTime.HasValue))
                {
                    var schoolClassForTime = await _context.SchoolClasses.FindAsync(session.ClassId);
                    if (schoolClassForTime != null && !string.IsNullOrEmpty(schoolClassForTime.Schedule))
                    {
                        var parts = schoolClassForTime.Schedule.Split(' ');
                        if (parts.Length > 1) 
                        {
                            var timeParts = parts[1].Split('-');
                            if (timeParts.Length == 2 && TimeSpan.TryParse(timeParts[0], out var st) && TimeSpan.TryParse(timeParts[1], out var et))
                            {
                                session.StartTime = st;
                                session.EndTime = et;
                            }
                        }
                    }
                }

                if (!session.StartTime.HasValue || !session.EndTime.HasValue)
                {
                    TempData["ErrorMessage"] = "Cannot create session: Could not determine Start Time and End Time.";
                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                }

                // Validation 1: Public Holiday
                var isHoliday = await _context.PublicHolidays.AnyAsync(h => h.Date.Date == session.SessionDate.Date);
                if (isHoliday)
                {
                    TempData["ErrorMessage"] = "Cannot create session: This date falls on a Public Holiday.";
                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                }

                // Validation 2: Time clash for this specific class
                var classClash = await _context.AttendanceSessions
                    .AnyAsync(s => s.ClassId == session.ClassId 
                                && s.SessionDate.Date == session.SessionDate.Date 
                                && s.StartTime < session.EndTime 
                                && s.EndTime > session.StartTime);
                                
                if (classClash)
                {
                    TempData["ErrorMessage"] = "Cannot create session: The selected time clashes with an existing session for this class.";
                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                }
                
                // Validation 3: Time clash for the teacher of this class
                var schoolClass = await _context.SchoolClasses.FindAsync(session.ClassId);
                var teacherId = schoolClass?.TeacherId;
                
                if (teacherId.HasValue)
                {
                    var teacherClash = await _context.AttendanceSessions
                        .Include(s => s.SchoolClass)
                        .AnyAsync(s => s.SchoolClass != null && s.SchoolClass.TeacherId == teacherId.Value 
                                    && s.SessionDate.Date == session.SessionDate.Date
                                    && s.StartTime < session.EndTime 
                                    && s.EndTime > session.StartTime);
                    
                    if (teacherClash)
                    {
                        TempData["ErrorMessage"] = "Cannot create session: The teacher is already teaching another class during this time slot.";
                        return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                    }

                    // Check against regular class schedules for this teacher
                    string dayOfWeek = session.SessionDate.ToString("dddd");
                    var teacherClasses = await _context.SchoolClasses.Where(c => c.TeacherId == teacherId.Value).ToListAsync();
                    
                    foreach(var tc in teacherClasses.Where(c => c.ClassId != session.ClassId))
                    {
                        if (string.IsNullOrEmpty(tc.Schedule)) continue;
                        var parts = tc.Schedule.Split(' ');
                        if (parts.Length == 2 && parts[0] == dayOfWeek)
                        {
                            var timeParts = parts[1].Split('-');
                            if (timeParts.Length == 2 && TimeSpan.TryParse(timeParts[0], out TimeSpan regStart) && TimeSpan.TryParse(timeParts[1], out TimeSpan regEnd))
                            {
                                if (regStart < session.EndTime && regEnd > session.StartTime)
                                {
                                    TempData["ErrorMessage"] = $"Cannot create session: The teacher has a regular class ({tc.ClassName}) scheduled at this time.";
                                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                                }
                            }
                        }
                    }
                }

                // Validation 4: Valid business hours
                if (session.StartTime >= session.EndTime)
                {
                    TempData["ErrorMessage"] = "Cannot create session: Start time must be before end time.";
                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                }
                
                if (session.StartTime.Value.Hours < 8 || session.EndTime.Value.Hours > 21 || (session.EndTime.Value.Hours == 21 && session.EndTime.Value.Minutes > 0))
                {
                    TempData["ErrorMessage"] = "Cannot create session: Time must be within operating hours (08:00 AM - 09:00 PM).";
                    return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
                }

                session.QRCodeToken = new Random().Next(100000, 999999).ToString();
                
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
                        Status = "Pending"
                    });
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
            }

            var classes = await GetAllowedClasses().OrderBy(c => c.ClassName).ToListAsync();
            ViewData["ClassId"] = new SelectList(classes, "ClassId", "ClassName", session.ClassId);
            return View(session);
        }

        // GET: Mark Attendance Grid
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Mark(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                    .ThenInclude(c => c!.Teacher)
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
        [Authorize(Roles = "Admin,Teacher")]
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

            return RedirectToAction("Details", "Class", new { id = session.ClassId }, "sessions-table");
        }

        // GET: Display QR Code for Attendance Check-in
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> QRCode(int id)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .Include(s => s.AttendanceRecords)
                    .ThenInclude(r => r.Student)
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

                // POST: Regenerate a new randomized 6-digit PIN code for a session
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateCode(int id)
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

            // Generate brand new randomized 6-digit numeric PIN
            session.QRCodeToken = new Random().Next(100000, 999999).ToString();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"New 6-digit attendance PIN generated: {session.QRCodeToken}";
            return RedirectToAction(nameof(QRCode), new { id });
        }

        // POST: PIN Check-In (Student registers attendance via 6-digit PIN)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> PinCheckIn(string pinCode)
        {
            if (string.IsNullOrWhiteSpace(pinCode))
            {
                TempData["ErrorMessage"] = "Please enter a valid 6-digit PIN code.";
                return RedirectToAction("Index", "Dashboard");
            }

            var cleanPin = pinCode.Trim();

            var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
            if (student == null)
            {
                var userFullName = User.FindFirstValue(ClaimTypes.Name) ?? "Student";
                student = new Student
                {
                    Name = userFullName,
                    Email = email ?? "",
                    Status = "Active"
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
            }

            var session = await _context.AttendanceSessions
                .Include(s => s.SchoolClass)
                .FirstOrDefaultAsync(s => s.QRCodeToken == cleanPin);

            if (session == null)
            {
                TempData["ErrorMessage"] = "Invalid 6-digit PIN code. Please verify the code with your teacher.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Verify or add enrollment
            var isEnrolled = await _context.Enrollments.AnyAsync(e => e.ClassId == session.ClassId && e.StudentId == student.StudentId);
            if (!isEnrolled)
            {
                _context.Enrollments.Add(new Enrollment
                {
                    ClassId = session.ClassId,
                    StudentId = student.StudentId,
                    EnrollDate = DateTime.Today
                });
                await _context.SaveChangesAsync();
            }

            var record = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.SessionId == session.SessionId && r.StudentId == student.StudentId);

            if (record != null)
            {
                if (record.Status == "Present")
                {
                    TempData["SuccessMessage"] = $"You have already checked in as Present for {session.SchoolClass?.ClassName}!";
                }
                else
                {
                    record.Status = "Present";
                    record.MarkedAt = DateTime.Now;
                    record.Remarks = "Checked in via 6-digit PIN code";
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Attendance registered successfully! You are marked Present for {session.SchoolClass?.ClassName} ({session.SessionType}).";
                }
            }
            else
            {
                record = new AttendanceRecord
                {
                    SessionId = session.SessionId,
                    StudentId = student.StudentId,
                    Status = "Present",
                    MarkedAt = DateTime.Now,
                    Remarks = "Checked in via 6-digit PIN code"
                };
                _context.AttendanceRecords.Add(record);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Attendance registered successfully! You are marked Present for {session.SchoolClass?.ClassName} ({session.SessionType}).";
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.AttendanceRecords)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
            {
                return NotFound();
            }

            int classId = session.ClassId;

            _context.AttendanceRecords.RemoveRange(session.AttendanceRecords);
            _context.AttendanceSessions.Remove(session);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Session deleted successfully.";
            return RedirectToAction("Details", "Class", new { id = classId }, "sessions-table");
        }
    }
}









