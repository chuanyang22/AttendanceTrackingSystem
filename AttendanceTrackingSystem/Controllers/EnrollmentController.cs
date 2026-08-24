using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EnrollmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Enrollment
        public async Task<IActionResult> Index(string searchString)
        {
            var enrollments = _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.SchoolClass)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                enrollments = enrollments.Where(e =>
                    (e.Student != null && e.Student.Name.Contains(searchString)) ||
                    (e.SchoolClass != null && e.SchoolClass.ClassName.Contains(searchString)));
            }

            ViewData["CurrentFilter"] = searchString;

            return View(await enrollments.OrderByDescending(e => e.EnrollDate).ToListAsync());

        }

        // GET: Enrollment/Create?studentId=1&classId=2
        public IActionResult Create(int? studentId, int? classId)
        {
            PopulateDropDowns(studentId, classId);
            var enrollment = new Enrollment();
            if (studentId.HasValue) enrollment.StudentId = studentId.Value;
            if (classId.HasValue) enrollment.ClassId = classId.Value;

            return View(enrollment);
        }

        // POST: Enrollment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,ClassId,EnrollDate")] Enrollment enrollment)
        {
            bool alreadyEnrolled = await _context.Enrollments.AnyAsync(e =>
                e.StudentId == enrollment.StudentId && e.ClassId == enrollment.ClassId);

            if (alreadyEnrolled)
            {
                ModelState.AddModelError(string.Empty, "This student is already enrolled in this class.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(enrollment);
                await _context.SaveChangesAsync();

                var schoolClass = await _context.SchoolClasses.FindAsync(enrollment.ClassId);
                if (schoolClass != null)
                {
                    return RedirectToAction("Details", "Class", new { id = enrollment.ClassId });
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateDropDowns(enrollment.StudentId, enrollment.ClassId);
            return View(enrollment);
        }

        // GET: Enrollment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.SchoolClass)
                .FirstOrDefaultAsync(e => e.EnrollmentId == id);

            if (enrollment == null) return NotFound();

            return View(enrollment);
        }

        // POST: Enrollment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var enrollment = await _context.Enrollments.FindAsync(id);
            int? classId = enrollment?.ClassId;

            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
            }

            if (classId.HasValue && await _context.SchoolClasses.AnyAsync(c => c.ClassId == classId))
            {
                return RedirectToAction("Details", "Class", new { id = classId });
            }

            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropDowns(int? selectedStudentId = null, int? selectedClassId = null)
        {
            ViewData["StudentId"] = new SelectList(
                _context.Students.OrderBy(s => s.Name), "StudentId", "Name", selectedStudentId);

            ViewData["ClassId"] = new SelectList(
                _context.SchoolClasses.OrderBy(c => c.ClassName), "ClassId", "ClassName", selectedClassId);
        }
    }
}


