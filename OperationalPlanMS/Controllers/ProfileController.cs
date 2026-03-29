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
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        // تشفير كلمات المرور
        private static readonly Microsoft.AspNetCore.Identity.PasswordHasher<Models.Entities.User> _passwordHasher = new();

        public ProfileController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Profile
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.SupervisedInitiatives.Where(i => !i.IsDeleted))
                .Include(u => u.ManagedProjects.Where(p => !p.IsDeleted))
                .Include(u => u.AssignedSteps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new ProfileViewModel
            {
                Id = user.Id,
                ADUsername = user.ADUsername,
                Email = user.Email,
                FullNameAr = user.FullNameAr,
                FullNameEn = user.FullNameEn,
                ProfileImage = user.ProfileImage,
                RoleName = user.Role?.NameAr,
                UnitName = user.ExternalUnitName,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                SupervisedInitiativesCount = user.SupervisedInitiatives?.Count ?? 0,
                ManagedProjectsCount = user.ManagedProjects?.Count ?? 0,
                AssignedStepsCount = user.AssignedSteps?.Count ?? 0,
                CompletedStepsCount = user.AssignedSteps?.Count(s => s.ProgressPercentage >= 100) ?? 0
            };

            return View(viewModel);
        }

        // GET: /Profile/Edit
        public async Task<IActionResult> Edit()
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new EditProfileViewModel
            {
                Id = user.Id,
                FullNameAr = user.FullNameAr,
                FullNameEn = user.FullNameEn,
                Email = user.Email
            };

            return View(viewModel);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            var userId = GetCurrentUserId();

            if (userId != model.Id)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                user.FullNameAr = model.FullNameAr;
                user.FullNameEn = model.FullNameEn;
                user.Email = model.Email;

                await _db.SaveChangesAsync();

                // تحديث الـ Claims
                await RefreshUserClaims(user);

                TempData["SuccessMessage"] = "تم تحديث البيانات الشخصية بنجاح";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: /Profile/ChangePassword
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: /Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = GetCurrentUserId();
                var user = await _db.Users.FindAsync(userId);

                if (user == null)
                {
                    return NotFound();
                }

                // التحقق من كلمة المرور الحالية
                bool currentPasswordValid = false;

                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    try
                    {
                        // محاولة التحقق كـ hash
                        var verifyResult = _passwordHasher.VerifyHashedPassword(
                            user, user.PasswordHash, model.CurrentPassword);

                        if (verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                            || verifyResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded)
                        {
                            currentPasswordValid = true;
                        }
                    }
                    catch (FormatException)
                    {
                        // PasswordHash ليس hash صالح — مخزن كنص عادي
                    }

                    // Fallback: مقارنة كنص عادي
                    if (!currentPasswordValid && user.PasswordHash == model.CurrentPassword)
                    {
                        currentPasswordValid = true;
                    }
                }

                if (!currentPasswordValid)
                {
                    ModelState.AddModelError("CurrentPassword", "كلمة المرور الحالية غير صحيحة");
                    return View(model);
                }

                // التحقق أن كلمة المرور الجديدة مختلفة
                if (model.CurrentPassword == model.NewPassword)
                {
                    ModelState.AddModelError("NewPassword", "كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية");
                    return View(model);
                }

                // تحديث كلمة المرور — مشفرة
                user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تغيير كلمة المرور بنجاح";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // POST: /Profile/UploadImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(IFormFile profileImage)
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            if (profileImage == null || profileImage.Length == 0)
            {
                TempData["ErrorMessage"] = "الرجاء اختيار صورة";
                return RedirectToAction(nameof(Index));
            }

            // التحقق من نوع الملف
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(profileImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] = "نوع الملف غير مدعوم. الأنواع المسموحة: JPG, PNG, GIF";
                return RedirectToAction(nameof(Index));
            }

            // التحقق من محتوى الملف (magic bytes)
            if (!await IsValidImageAsync(profileImage))
            {
                TempData["ErrorMessage"] = "محتوى الملف لا يطابق نوع الصورة المتوقع";
                return RedirectToAction(nameof(Index));
            }

            // التحقق من حجم الملف (max 2MB)
            if (profileImage.Length > 2 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "حجم الصورة يجب أن لا يتجاوز 2 ميجابايت";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // إنشاء مجلد الصور إذا لم يكن موجوداً
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // حذف الصورة القديمة إذا وجدت
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, user.ProfileImage.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                // إنشاء اسم فريد للملف
                var fileName = $"profile_{userId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // حفظ الملف
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                // تحديث مسار الصورة في قاعدة البيانات
                user.ProfileImage = $"/uploads/profiles/{fileName}";
                await _db.SaveChangesAsync();

                // تحديث الـ Claims لتظهر الصورة فوراً
                await RefreshUserClaims(user);

                TempData["SuccessMessage"] = "تم تحديث الصورة الشخصية بنجاح";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "حدث خطأ أثناء رفع الصورة";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/RemoveImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveImage()
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(user.ProfileImage))
            {
                // حذف الصورة من الخادم
                var imagePath = Path.Combine(_env.WebRootPath, user.ProfileImage.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }

                // إزالة المسار من قاعدة البيانات
                user.ProfileImage = null;
                await _db.SaveChangesAsync();

                // تحديث الـ Claims
                await RefreshUserClaims(user);

                TempData["SuccessMessage"] = "تم حذف الصورة الشخصية";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// تحديث الـ Claims للمستخدم الحالي
        /// </summary>
        private async Task RefreshUserClaims(Models.Entities.User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullNameAr),
                new Claim("FullNameAr", user.FullNameAr),
                new Claim("FullNameEn", user.FullNameEn),
                new Claim(ClaimTypes.Role, ((Models.UserRole)user.RoleId).ToString()),
                new Claim("RoleId", user.RoleId.ToString()),
                new Claim("RoleNameAr", user.Role?.NameAr ?? ""),
                new Claim("RoleNameEn", user.Role?.NameEn ?? ""),
                new Claim("EmployeeRank", user.EmployeeRank ?? ""),
            };

            if (user.ExternalUnitId.HasValue)
            {
                claims.Add(new Claim("ExternalUnitId", user.ExternalUnitId.Value.ToString()));
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            if (!string.IsNullOrEmpty(user.ProfileImage))
            {
                claims.Add(new Claim("ProfileImage", user.ProfileImage));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal);
        }

        /// <summary>
        /// التحقق من محتوى الملف عبر magic bytes
        /// </summary>
        private static async Task<bool> IsValidImageAsync(IFormFile file)
        {
            if (file.Length < 4) return false;

            var buffer = new byte[8];
            using var stream = file.OpenReadStream();
            await stream.ReadAsync(buffer, 0, buffer.Length);

            // JPEG: FF D8 FF
            if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                return true;

            // GIF: 47 49 46 38
            if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
                return true;

            return false;
        }
    }
}