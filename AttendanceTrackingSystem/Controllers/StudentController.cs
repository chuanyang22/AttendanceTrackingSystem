using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Student
        public async Task<IActionResult> Index(string searchString)
        {
            var students = from s in _context.Students select s;

            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.Name.Contains(searchString)
                                             || s.Email.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;

            var result = await students.ToListAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_StudentTable", result);
            }

            return View(result);
        }

        // GET: Student/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.SchoolClass)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // GET: Student/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Email,Phone,Status")] Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Add(student);
                
                // Auto-create a User account for this student so they can log in
                bool emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == student.Email.ToLower());
                if (!emailExists)
                {
                    var newUser = new User
                    {
                        FullName = student.Name,
                        Email = student.Email,
                        Role = "Student",
                        PasswordHash = AttendanceTrackingSystem.Services.PasswordHelper.HashPassword("Student123!"), // Default password
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(newUser);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }
        // GET: Student/BatchCreate
        public IActionResult BatchCreate()
        {
            // Start with 3 blank rows for the user to fill in
            var students = new List<Student> { new Student(), new Student(), new Student() };
            return View(students);
        }

        // POST: Student/BatchCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchCreate(List<Student> students)
        {
            // Ignore completely blank rows (user didn't fill them in)
            var rowsToProcess = new List<Student>();
            for (int i = 0; i < students.Count; i++)
            {
                var s = students[i];
                bool isBlank = string.IsNullOrWhiteSpace(s.Name)
                    && string.IsNullOrWhiteSpace(s.Email)
                    && string.IsNullOrWhiteSpace(s.Phone);

                if (isBlank)
                {
                    ModelState.Remove($"students[{i}].Name");
                    ModelState.Remove($"students[{i}].Email");
                    ModelState.Remove($"students[{i}].Phone");
                    ModelState.Remove($"students[{i}].Status");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(s.Status)) s.Status = "Active";
                rowsToProcess.Add(s);
            }

            if (rowsToProcess.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Please fill in at least one student row.");
                return View(students);
            }

            if (!ModelState.IsValid)
            {
                return View(students);
            }

            _context.Students.AddRange(rowsToProcess);

            // Create User accounts for these new students
            var existingEmails = await _context.Users.Select(u => u.Email.ToLower()).ToListAsync();
            foreach (var student in rowsToProcess)
            {
                if (!existingEmails.Contains(student.Email.ToLower()))
                {
                    _context.Users.Add(new User
                    {
                        FullName = student.Name,
                        Email = student.Email,
                        Role = "Student",
                        PasswordHash = AttendanceTrackingSystem.Services.PasswordHelper.HashPassword("Student123!"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                    existingEmails.Add(student.Email.ToLower()); // Prevent duplicates in same batch
                }
            }

            await _context.SaveChangesAsync();

            TempData["BatchSuccessMessage"] = $"{rowsToProcess.Count} student(s) added successfully.";
            return RedirectToAction(nameof(Index));
        }


        // GET: Student/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Student/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,Name,Email,Phone,Status")] Student student)
        {
            if (id != student.StudentId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Get the original student to know the old email before updating
                    var existingStudent = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == id);
                    if (existingStudent == null) return NotFound();
                    
                    var oldEmail = existingStudent.Email;

                    // 2. Update the Student record
                    _context.Update(student);
                    
                    // 3. Find the corresponding User and update their name/email too
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == oldEmail.ToLower() && u.Role == "Student");
                    if (user != null)
                    {
                        user.FullName = student.Name;
                        user.Email = student.Email; // Sync email if it was changed
                        _context.Users.Update(user);
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Students.Any(e => e.StudentId == student.StudentId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Student/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == id);
            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Student/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == student.Email.ToLower());
                if (user != null)
                {
                    _context.Users.Remove(user);
                }

                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}


