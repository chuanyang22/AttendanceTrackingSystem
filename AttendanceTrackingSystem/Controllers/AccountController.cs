using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Services;
using AttendanceTrackingSystem.Data;
using AttendanceTrackingSystem.Models;
using AttendanceTrackingSystem.Models.ViewModels;

namespace AttendanceTrackingSystem.Controllers
{
    public class AccountController : Controller
    {
                private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly AttendanceTrackingSystem.Services.IEmailService _emailService;

        public AccountController(ApplicationDbContext context, IWebHostEnvironment environment, AttendanceTrackingSystem.Services.IEmailService emailService)
        {
            _context = context;
            _environment = environment;
            _emailService = emailService;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login (With 3-failed-attempts Temporary Lockout)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Check Lockout Status
            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((user.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
                ModelState.AddModelError(string.Empty, $"Account locked due to 3 failed attempts. Try again in {remainingMinutes} minute(s).");
                return View(model);
            }

            // Verify Password Hash
            bool isValidPassword = PasswordHelper.VerifyPassword(model.Password, user.PasswordHash);

            if (!isValidPassword)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(5); // Lock out for 5 minutes
                    user.FailedLoginAttempts = 0;
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "Account locked for 5 minutes due to 3 consecutive failed login attempts.");
                    return View(model);
                }

                await _context.SaveChangesAsync();
                int remaining = 3 - user.FailedLoginAttempts;
                ModelState.AddModelError(string.Empty, $"Invalid email or password. {remaining} attempt(s) remaining before temporary lockout.");
                return View(model);
            }

            // Reset failed counter upon successful login
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            await _context.SaveChangesAsync();

            // Set Cookie Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("ProfilePicture", user.ProfilePictureUrl ?? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FullName)}&background=random&color=fff")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }


        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

                // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (user != null)
            {
                var random = new Random();
                var pinCode = random.Next(100000, 999999).ToString();

                user.ResetToken = pinCode;
                user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();

                await _emailService.SendPasswordResetPinAsync(user.Email, user.FullName, pinCode);

                TempData["SuccessMessage"] = "A 6-digit PIN has been sent to your email.";
                return RedirectToAction(nameof(VerifyPin), new { email = model.Email });
            }

            TempData["SuccessMessage"] = "If an account with that email exists, we have sent a password reset PIN.";
            return RedirectToAction(nameof(VerifyPin), new { email = model.Email });
        }

        // GET: /Account/VerifyPin
        [HttpGet]
        public IActionResult VerifyPin(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction(nameof(Login));
            return View(new AttendanceTrackingSystem.Models.ViewModels.VerifyPinViewModel { Email = email });
        }

        // POST: /Account/VerifyPin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPin(AttendanceTrackingSystem.Models.ViewModels.VerifyPinViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (user != null)
            {
                if (user.ResetToken == model.PinCode && user.ResetTokenExpiry.HasValue && user.ResetTokenExpiry.Value > DateTime.UtcNow)
                {
                    var secureToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("+", "").Replace("/", "").Replace("=", "");
                    user.ResetToken = secureToken;
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(ResetPassword), new { token = secureToken, email = model.Email });
                }
            }

            ModelState.AddModelError("PinCode", "Invalid or expired PIN code.");
            return View(model);
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid password reset token.";
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

                // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (user == null || user.ResetToken != model.Token || !user.ResetTokenExpiry.HasValue || user.ResetTokenExpiry.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "The reset token has expired or is invalid. Please submit a new request.");
                return View(model);
            }

            user.PasswordHash = AttendanceTrackingSystem.Services.PasswordHelper.HashPassword(model.NewPassword);
            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            TempData.Remove($"ResetToken_{model.Email.ToLower()}");
            TempData.Remove($"ResetExpiry_{model.Email.ToLower()}");

            TempData["SuccessMessage"] = "Your password has been reset successfully! You can now log in.";
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/EditProfile
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            var vm = new EditProfileViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                ExistingProfilePictureUrl = user.ProfilePictureUrl
            };

            return View(vm);
        }

        // POST: /Account/EditProfile
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;

            if (user.Role == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == user.Email);
                if (student != null)
                {
                    student.Name = model.FullName;
                    _context.Students.Update(student);
                }
            }

            // Handle updated photo
            if (model.NewProfileImage != null && model.NewProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.NewProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewProfileImage.CopyToAsync(fileStream);
                }

                user.ProfilePictureUrl = $"/uploads/avatars/{uniqueFileName}";
            }

            await _context.SaveChangesAsync();

            // Refresh login cookie with updated name & avatar
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("ProfilePicture", user.ProfilePictureUrl ?? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.FullName)}&background=random&color=fff")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Index", "Dashboard");
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}






