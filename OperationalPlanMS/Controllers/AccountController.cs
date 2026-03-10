using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.ViewModels;
using System.Security.Claims;

namespace OperationalPlanMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to home
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by username
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ADUsername == model.Username && u.IsActive);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
                return View(model);
            }

            // Simple password check for demo (admin/admin123)
            // In production, use proper password hashing
            bool isValidPassword = false;

            if (model.Username.ToLower() == "admin" && model.Password == "admin123")
            {
                isValidPassword = true;
            }
            else if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash == model.Password)
            {
                // Simple check - in production use BCrypt or similar
                isValidPassword = true;
            }

            if (!isValidPassword)
            {
                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
                return View(model);
            }

            // Create claims for the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                if (user.ExternalUnitId.HasValue)
                    claims.Add(new Claim("ExternalUnitId", user.ExternalUnitId.Value.ToString()));
                new Claim(ClaimTypes.Name, user.FullNameAr),
                new Claim("FullNameAr", user.FullNameAr),
                new Claim("FullNameEn", user.FullNameEn),
                new Claim(ClaimTypes.Role, ((Models.UserRole)user.RoleId).ToString()),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("RoleNameAr", user.Role?.NameAr ?? ""),
                new Claim("RoleNameEn", user.Role?.NameEn ?? ""),
            };

            {
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            // إضافة الصورة الشخصية
            if (!string.IsNullOrEmpty(user.ProfileImage))
            {
                claims.Add(new Claim("ProfileImage", user.ProfileImage));
            }
            // إضافة صلاحية مؤكد الخطوات
            claims.Add(new Claim("IsStepApprover", user.IsStepApprover.ToString()));


            // Create identity and principal
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Authentication properties
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            // Sign in the user
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            // Update last login time
            user.LastLoginAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // Redirect to return URL or home
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: /Account/Logout (for convenience)
        [HttpGet]
        public async Task<IActionResult> LogoutGet()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}