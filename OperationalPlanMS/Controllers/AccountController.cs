using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.ViewModels;
using System.Security.Claims;

namespace OperationalPlanMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AccountController> _logger;

        // تشفير كلمات المرور
        private static readonly Microsoft.AspNetCore.Identity.PasswordHasher<Models.Entities.User> _passwordHasher = new();

        public AccountController(AppDbContext db, IConfiguration config, ILogger<AccountController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ADUsername == model.Username && u.IsActive);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "رقم الموظف غير موجود أو الحساب غير نشط");
                return View(model);
            }

            bool isAuthenticated = false;
            var adEnabled = _config.GetValue<bool>("ActiveDirectory:Enabled");

            if (adEnabled)
            {
                // AD Authentication الحقيقي
                isAuthenticated = ValidateWithActiveDirectory(model.Username, model.Password);
            }
            else
            {
                // Fallback للتطوير - التحقق من كلمة المرور المخزنة
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    try
                    {
                        // أولاً: محاولة التحقق كـ hash مشفر
                        var verifyResult = _passwordHasher.VerifyHashedPassword(
                            user, user.PasswordHash, model.Password);

                        if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                            || verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded)
                        {
                            isAuthenticated = true;

                            // إعادة تشفير إذا الخوارزمية قديمة
                            if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded)
                            {
                                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                                await _db.SaveChangesAsync();
                            }
                        }
                    }
                    catch (FormatException)
                    {
                        // PasswordHash ليس hash صالح — يعني مخزن كنص عادي
                    }

                    // Fallback: إذا ما تم التحقق بعد — مقارنة كنص عادي
                    if (!isAuthenticated && user.PasswordHash == model.Password)
                    {
                        // كلمة مرور قديمة (نص عادي) — تحويلها تلقائياً لـ hash
                        isAuthenticated = true;
                        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("تم تحويل كلمة مرور المستخدم {Username} من نص عادي إلى hash", user.ADUsername);
                    }
                }
            }

            if (!isAuthenticated)
            {
                ModelState.AddModelError(string.Empty, "كلمة المرور غير صحيحة");
                return View(model);
            }

            await SignInUser(user, model.RememberMe);

            user.LastLoginAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // ربط المشاريع والخطوات التي عُيِّن عليها هذا الموظف قبل إنشاء حسابه
            await SyncUserAssignments(user.Id, user.ADUsername);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        private bool ValidateWithActiveDirectory(string username, string password)
        {
            try
            {
                var domain = _config["ActiveDirectory:Domain"] ?? "";
                var ldapPath = _config["ActiveDirectory:LdapPath"] ?? "";
                var domainUsername = $"{domain}\\{username}";

                using var entry = new System.DirectoryServices.DirectoryEntry(ldapPath, domainUsername, password);
                using var searcher = new System.DirectoryServices.DirectorySearcher(entry);
                searcher.Filter = $"(sAMAccountName={username})";
                searcher.PropertiesToLoad.Add("displayName");

                var result = searcher.FindOne();
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        private async Task SignInUser(Models.Entities.User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullNameAr),
                new Claim("FullNameAr", user.FullNameAr),
                new Claim("FullNameEn", user.FullNameEn ?? user.FullNameAr),
                new Claim(ClaimTypes.Role, ((Models.UserRole)user.RoleId).ToString()),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("RoleNameAr", user.Role?.NameAr ?? ""),
                new Claim("RoleNameEn", user.Role?.NameEn ?? ""),
                new Claim("IsStepApprover", user.IsStepApprover.ToString()),
            };

            if (user.ExternalUnitId.HasValue)
                claims.Add(new Claim("ExternalUnitId", user.ExternalUnitId.Value.ToString()));

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            if (!string.IsNullOrEmpty(user.ProfileImage))
                claims.Add(new Claim("ProfileImage", user.ProfileImage));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        /// <summary>
        /// عند تسجيل الدخول — يربط المشاريع والخطوات التي عُيِّن عليها الموظف
        /// قبل إنشاء حسابه في النظام (AssignedToId / ProjectManagerId كانت null)
        /// </summary>
        private async Task SyncUserAssignments(int userId, string empNumber)
        {
            try
            {
                // ربط مشاريع لم تُربط بعد
                await _db.Projects
                    .Where(p => p.ProjectManagerEmpNumber == empNumber
                             && p.ProjectManagerId == null
                             && !p.IsDeleted)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProjectManagerId, userId));

                // ربط خطوات لم تُربط بعد
                await _db.Steps
                    .Where(s => s.AssignedToEmpNumber == empNumber
                             && s.AssignedToId == null
                             && !s.IsDeleted)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.AssignedToId, userId));
            }
            catch
            {
                // لا نوقف تسجيل الدخول إذا فشل الـ sync
            }
        }

    }
}