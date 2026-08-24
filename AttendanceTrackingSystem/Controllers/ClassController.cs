using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize]
    public class ClassController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassController(ApplicationDbContext context)
        {
            _context = context;
        }

        private void PopulateTeachersDropDownList(object? selectedTeacher = null)
        {
            var teachersQuery = from u in _context.Users
                                where u.Role == "Teacher"
                                orderby u.FullName
                                select u;
            ViewBag.TeacherId = new SelectList(teachersQuery.AsNoTracking(), "UserId", "FullName", selectedTeacher);
        }

        // GET: Class
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            var classes = _context.SchoolClasses.Include(c => c.Teacher).AsQueryable();

            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    classes = classes.Where(c => c.TeacherId == userId);
                }
            }
            else if (User.IsInRole("Student"))
            {
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToLower();
                classes = classes.Where(c => c.Enrollments.Any(e => e.Student.Email.ToLower() == email));
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                classes = classes.Where(c => c.ClassName.Contains(searchString)
                                           || (c.Teacher != null && c.Teacher.FullName.Contains(searchString)));
            }

            ViewData["CurrentFilter"] = searchString;

            var result = await classes.ToListAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ClassTable", result);
            }

            return View(result);
        }

        // GET: Class/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var schoolClass = await _context.SchoolClasses
                .Include(c => c.Teacher)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                        .ThenInclude(s => s.AttendanceRecords)
                            .ThenInclude(ar => ar.AttendanceSession)
                .Include(c => c.AttendanceSessions)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (schoolClass == null) return NotFound();

            // Additional security: if Teacher, only view if it's their class
            if (User.IsInRole("Teacher"))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId) && schoolClass.TeacherId != userId)
                {
                    return Forbid();
                }
            }

            return View(schoolClass);
        }

        // GET: Class/Create (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            PopulateTeachersDropDownList();
            return View();
        }

        // POST: Class/Create (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ClassName,Schedule,TeacherId")] SchoolClass schoolClass)
        {
            if (!string.IsNullOrEmpty(schoolClass.Schedule))
            {
                var parts = schoolClass.Schedule.Split(new[] { ' ', '-' });
                if (parts.Length == 3 && TimeSpan.TryParse(parts[1], out TimeSpan start) && TimeSpan.TryParse(parts[2], out TimeSpan end))
                {
                    if ((end - start).TotalHours < 1)
                        ModelState.AddModelError("Schedule", "Class duration must be at least 1 hour.");
                    else if ((end - start).TotalHours > 4)
                        ModelState.AddModelError("Schedule", "Class duration cannot exceed 4 hours.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(schoolClass);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateTeachersDropDownList(schoolClass.TeacherId);
            return View(schoolClass);
        }

        // GET: Class/Edit/5 (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var schoolClass = await _context.SchoolClasses.FindAsync(id);
            if (schoolClass == null) return NotFound();

            PopulateTeachersDropDownList(schoolClass.TeacherId);
            return View(schoolClass);
        }

        // POST: Class/Edit/5 (Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("ClassId,ClassName,Schedule,TeacherId")] SchoolClass schoolClass)
        {
            if (id != schoolClass.ClassId) return NotFound();

            if (!string.IsNullOrEmpty(schoolClass.Schedule))
            {
                var parts = schoolClass.Schedule.Split(new[] { ' ', '-' });
                if (parts.Length == 3 && TimeSpan.TryParse(parts[1], out TimeSpan start) && TimeSpan.TryParse(parts[2], out TimeSpan end))
                {
                    if ((end - start).TotalHours < 1)
                        ModelState.AddModelError("Schedule", "Class duration must be at least 1 hour.");
                    else if ((end - start).TotalHours > 4)
                        ModelState.AddModelError("Schedule", "Class duration cannot exceed 4 hours.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(schoolClass);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.SchoolClasses.Any(e => e.ClassId == schoolClass.ClassId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PopulateTeachersDropDownList(schoolClass.TeacherId);
            return View(schoolClass);
        }

        // GET: Class/Delete/5 (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var schoolClass = await _context.SchoolClasses
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.ClassId == id);
            if (schoolClass == null) return NotFound();

            return View(schoolClass);
        }

        // POST: Class/Delete/5 (Admin only)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schoolClass = await _context.SchoolClasses.FindAsync(id);
            if (schoolClass != null)
            {
                _context.SchoolClasses.Remove(schoolClass);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
