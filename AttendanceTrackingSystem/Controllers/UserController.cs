using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Services;

namespace AttendanceTrackingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UserController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? role, string? searchString)
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.Role == role);
                ViewData["CurrentRole"] = role;
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(u => u.FullName.Contains(searchString) || u.Email.Contains(searchString));
                ViewData["CurrentFilter"] = searchString;
            }
            var users = await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
            return View(users);
        }

        // GET: /User/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user, string password, IFormFile? profileImage)
        {
            ModelState.Remove("PasswordHash");
            ModelState.Remove("IsActive");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ModelState.AddModelError("password", "Password must be at least 6 characters long.");
            }

            bool emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == user.Email.ToLower());
            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email address is already in use.");
            }

            if (ModelState.IsValid)
            {
                if (profileImage != null && profileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(profileImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(fileStream);
                    }
                    user.ProfilePictureUrl = $"/uploads/avatars/{uniqueFileName}";
                }

                user.PasswordHash = PasswordHelper.HashPassword(password);
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (user.Role == "Student")
                {
                    var student = new Student
                    {
                        Name = user.FullName,
                        Email = user.Email,
                        Status = "Active"
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "User created successfully!";
                return RedirectToAction(nameof(Index), new { role = user.Role });
            }

            return View(user);
        }

        // GET: /User/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: /User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User model, string? newPassword, IFormFile? profileImage)
        {
            ModelState.Remove("PasswordHash");
            ModelState.Remove("IsActive");

            if (id != model.UserId) return NotFound();

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null) return NotFound();

            bool emailConflict = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower() && u.UserId != id);
            if (emailConflict)
            {
                ModelState.AddModelError("Email", "This email address is already taken by another account.");
            }

            if (ModelState.IsValid)
            {
                var oldEmail = existingUser.Email;
                existingUser.FullName = model.FullName;
                existingUser.Email = model.Email;
                existingUser.Role = model.Role;
                existingUser.IsActive = model.IsActive;

                if (existingUser.Role == "Student")
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == oldEmail);
                    if (student != null)
                    {
                        student.Name = model.FullName;
                        student.Email = model.Email;
                        _context.Students.Update(student);
                    }
                }

                // Update password only if provided
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    if (newPassword.Length >= 6)
                    {
                        existingUser.PasswordHash = PasswordHelper.HashPassword(newPassword);
                    }
                    else
                    {
                        ModelState.AddModelError("newPassword", "New password must be at least 6 characters.");
                        return View(model);
                    }
                }

                // Update profile photo if new file uploaded
                if (profileImage != null && profileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(profileImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(fileStream);
                    }
                    existingUser.ProfilePictureUrl = $"/uploads/avatars/{uniqueFileName}";
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User updated successfully!";
                return RedirectToAction(nameof(Index), new { role = existingUser.Role });
            }

            return View(model);
        }

        // POST: /User/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            string? role = null;
            if (user != null)
            {
                role = user.Role;
                if (user.Role == "Student")
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Email.ToLower() == user.Email.ToLower());
                    if (student != null)
                    {
                        _context.Students.Remove(student);
                    }
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User deleted successfully!";
            }
            return RedirectToAction(nameof(Index), new { role = role });
        }
    }
}
