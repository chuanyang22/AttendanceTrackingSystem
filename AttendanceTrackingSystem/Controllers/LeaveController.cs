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

        // GET: Leave (Page for Student Application & Teacher/Admin Review)
        [HttpGet]
        public async Task<IActionResult> Index(string statusFilter = "All", string searchString = "")
        {
            var model = new LeaveIndexViewModel
            {
                IsStudent = User.IsInRole("Student"),
                StatusFilter = statusFilter,
                SearchString = searchString
            };

            if (model.IsStudent)
            {
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
                if (student == null && !string.IsNullOrEmpty(email))
                {
                    var prefix = email.Split('@')[0].Split('-')[0];
                    student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower().StartsWith(prefix));
                }

                if (student != null)
                {
                    model.MyApplications = await _context.LeaveApplications
                        .Include(l => l.ReviewedByUser)
                        .Where(l => l.StudentId == student.StudentId)
                        .OrderByDescending(l => l.SubmittedAt)
                        .ToListAsync();
                }
            }
            else
            {
                // Admin & Teacher View - Both can view, verify, and approve/reject student leave requests
                var query = _context.LeaveApplications
                    .Include(l => l.Student)
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

                if (!string.IsNullOrEmpty(searchString))
                {
                    var search = searchString.ToLower();
                    query = query.Where(l => (l.Student != null && l.Student.Name.ToLower().Contains(search)) 
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

        // POST: Student Apply for Leave
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Apply(LeaveIndexViewModel indexModel)
        {
            var form = indexModel.ApplyForm;
            var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == email);
            if (student == null && !string.IsNullOrEmpty(email))
            {
                var prefix = email.Split('@')[0].Split('-')[0];
                student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower().StartsWith(prefix));
            }

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student profile not found. Please contact administration.";
                return RedirectToAction(nameof(Index));
            }

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
                StudentId = student.StudentId,
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

            TempData["SuccessMessage"] = "Your leave application has been submitted successfully and is now pending review.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Approve Leave Application
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Approve(int id, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.Student)
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

            // Auto-mark attendance records within leave dates as 'Excused'
            var recordsToExcuse = await _context.AttendanceRecords
                .Include(r => r.AttendanceSession)
                .Where(r => r.StudentId == leave.StudentId 
                          && r.AttendanceSession != null
                          && r.AttendanceSession.SessionDate.Date >= leave.StartDate.Date 
                          && r.AttendanceSession.SessionDate.Date <= leave.EndDate.Date)
                .ToListAsync();

            foreach (var rec in recordsToExcuse)
            {
                rec.Status = "Excused";
                rec.Remarks = $"Approved Leave #{leave.LeaveApplicationId}";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Leave application for {leave.Student?.Name} has been APPROVED.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Reject Leave Application
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Reject(int id, string? remarks)
        {
            var leave = await _context.LeaveApplications
                .Include(l => l.Student)
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

            TempData["ErrorMessage"] = $"Leave application for {leave.Student?.Name} has been REJECTED.";
            return RedirectToAction(nameof(Index));
        }
    }
}