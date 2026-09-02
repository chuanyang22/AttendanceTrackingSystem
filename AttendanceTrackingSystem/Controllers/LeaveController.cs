using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LeaveController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Leave (Page for Student/Teacher Application & Admin Review)
        [HttpGet]
        public async Task<IActionResult> Index(string statusFilter = "All", string roleFilter = "All", string searchString = "")
        {
            var isAdmin = User.IsInRole("Admin");
            var userRole = isAdmin ? "Admin" : (User.IsInRole("Teacher") ? "Teacher" : "Student");

            var model = new LeaveIndexViewModel
            {
                IsAdmin = isAdmin,
                IsApplicant = !isAdmin,
                UserRole = userRole,
                StatusFilter = statusFilter,
                RoleFilter = roleFilter,
                SearchString = searchString
            };

            if (!isAdmin)
            {
                // Applicant View (Student or Teacher)
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int.TryParse(userIdStr, out int userId);
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();

                // Find student ID if student
                int? studentId = null;
                if (userRole == "Student" && !string.IsNullOrEmpty(email))
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
                    if (student == null)
                    {
                        var prefix = email.Split('@')[0].Split('-')[0];
                        student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower().StartsWith(prefix));
                    }
                    studentId = student?.StudentId;
                }

                model.MyApplications = await _context.LeaveApplications
                    .Include(l => l.ReviewedByUser)
                    .Where(l => (userId > 0 && l.UserId == userId) || (studentId.HasValue && l.StudentId == studentId.Value))
                    .OrderByDescending(l => l.SubmittedAt)
                    .ToListAsync();
            }
            else
            {
                // Admin Review View - Only Admin can review and approve/reject all leave applications
                var query = _context.LeaveApplications
                    .Include(l => l.Student)
                    .Include(l => l.ApplicantUser)
                    .Include(l => l.ReviewedByUser)
                    .AsQueryable();

                model.TotalCount = await query.CountAsync();
                model.PendingCount = await query.CountAsync(l => l.Status == "Pending");
                model.ApprovedCount = await query.CountAsync(l => l.Status == "Approved");
                model.RejectedCount = await query.CountAsync(l => l.Status == "Rejected");

                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
                {
                    query = query.Where(l => l.Status == statusFilter);
                }

                if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All")
                {
                    query = query.Where(l => l.ApplicantRole == roleFilter);
                }

                if (!string.IsNullOrEmpty(searchString))
                {
                    var search = searchString.ToLower();
                    query = query.Where(l => l.ApplicantName.ToLower().Contains(search)
                                         || (l.ApplicantUser != null && l.ApplicantUser.Email.ToLower().Contains(search))
                                         || (l.Student != null && l.Student.Email.ToLower().Contains(search))
                                         || l.Reason.ToLower().Contains(search));
                }

                model.Applications = await query
                    .OrderBy(l => l.Status == "Pending" ? 0 : 1)
                    .ThenByDescending(l => l.SubmittedAt)
                    .ToListAsync();
            }

            return View(model);
        }

        // POST: Student or Teacher Apply for Leave
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student,Teacher")]
        public async Task<IActionResult> Apply(LeaveIndexViewModel indexModel)
        {
            var form = indexModel.ApplyForm;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int userId);
            var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
            var isTeacher = User.IsInRole("Teacher");
            var applicantRole = isTeacher ? "Teacher" : "Student";

            var user = await _context.Users.FindAsync(userId);
            Student? student = null;
            if (!isTeacher && !string.IsNullOrEmpty(email))
            {
                student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
                if (student == null)
                {
                    var prefix = email.Split('@')[0].Split('-')[0];
                    student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower().StartsWith(prefix));
                }
            }

            var applicantName = isTeacher ? (user?.FullName ?? "Teacher") : (student?.Name ?? user?.FullName ?? "Student");

            // Validate dates range
            if (form.EndDate < form.StartDate)
            {
                TempData["ErrorMessage"] = "To Date must be greater than or equal to From Date.";
                return RedirectToAction(nameof(Index));
            }

            // Validate Proof Document
            if (form.ProofFile == null || form.ProofFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please upload a supporting proof document (PDF, JPG, or PNG).";
                return RedirectToAction(nameof(Index));
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(form.ProofFile.FileName)?.ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                TempData["FileFormatError"] = "Error: Invalid file format. Only PDF, JPEG, JPG, or PNG files are accepted.";
                TempData["ErrorMessage"] = "Error: Invalid file format. Only PDF, JPEG, JPG, or PNG files are accepted.";
                return RedirectToAction(nameof(Index));
            }

            // Max 10MB
            if (form.ProofFile.Length > 10 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "File size exceeds the 10MB limit.";
                return RedirectToAction(nameof(Index));
            }

            // Save file to wwwroot/uploads/leave_proofs/
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "leave_proofs");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString("N") + extension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await form.ProofFile.CopyToAsync(fileStream);
            }

            var leaveApplication = new LeaveApplication
            {
                UserId = userId > 0 ? userId : null,
                StudentId = student?.StudentId,
                ApplicantName = applicantName,
                ApplicantRole = applicantRole,
                StartDate = form.StartDate.Date,
                EndDate = form.EndDate.Date,
                Reason = form.Reason,
                Description = form.Description,
                ProofDocumentUrl = "/uploads/leave_proofs/" + uniqueFileName,
                ProofDocumentFileName = form.ProofFile.FileName,
                Status = "Pending",
                SubmittedAt = DateTime.Now
            };

            _context.LeaveApplications.Add(leaveApplication);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your leave application has been submitted successfully and is now pending admin review.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Approve Leave Application (Admin Only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.Student)
                .Include(l => l.ApplicantUser)
                .FirstOrDefaultAsync(l => l.LeaveApplicationId == id);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToAction(nameof(Index));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdStr, out int uId) ? uId : null;

            leave.Status = "Approved";
            leave.ReviewedByUserId = userId;
            leave.ReviewedAt = DateTime.Now;
            leave.ReviewerRemarks = string.IsNullOrWhiteSpace(remarks) ? "Approved" : remarks;

            // Auto-mark attendance records within leave dates as 'Excused' if student
            if (leave.StudentId.HasValue)
            {
                var recordsToExcuse = await _context.AttendanceRecords
                    .Include(r => r.AttendanceSession)
                    .Where(r => r.StudentId == leave.StudentId.Value 
                              && r.AttendanceSession != null
                              && r.AttendanceSession.SessionDate.Date >= leave.StartDate.Date 
                              && r.AttendanceSession.SessionDate.Date <= leave.EndDate.Date)
                    .ToListAsync();

                foreach (var rec in recordsToExcuse)
                {
                    rec.Status = "Excused";
                    rec.Remarks = $"Approved Leave #{leave.LeaveApplicationId}";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Leave application for {leave.ApplicantName} ({leave.ApplicantRole}) has been APPROVED.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Reject Leave Application (Admin Only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.Student)
                .Include(l => l.ApplicantUser)
                .FirstOrDefaultAsync(l => l.LeaveApplicationId == id);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToAction(nameof(Index));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdStr, out int uId) ? uId : null;

            leave.Status = "Rejected";
            leave.ReviewedByUserId = userId;
            leave.ReviewedAt = DateTime.Now;
            leave.ReviewerRemarks = string.IsNullOrWhiteSpace(remarks) ? "Rejected" : remarks;

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = $"Leave application for {leave.ApplicantName} ({leave.ApplicantRole}) has been REJECTED.";
            return RedirectToAction(nameof(Index));
        }
    }
}