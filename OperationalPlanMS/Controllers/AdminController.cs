using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;

        public AdminController(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        // التحقق من صلاحية الوصول
        private bool IsAdminUser()
        {
            return GetCurrentUserRole() == UserRole.Admin;
        }

        // الصفحة الرئيسية للإدارة
        public IActionResult Index()
        {
            if (!IsAdminUser())
                return Forbid();

            return View();
        }

                        #region Users

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? searchTerm, int? roleId, int? organizationalUnitId, bool? isActive, int page = 1)
        {
            if (!IsAdminUser())
                return Forbid();

            var query = _db.Users
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => u.FullNameAr.Contains(searchTerm) ||
                                        u.FullNameEn.Contains(searchTerm) ||
                                        u.ADUsername.Contains(searchTerm) ||
                                        u.Email!.Contains(searchTerm));
            }

            if (roleId.HasValue)
                query = query.Where(u => u.RoleId == roleId.Value);

            if (organizationalUnitId.HasValue)
                query = query.Where(u => u.OrganizationalUnitId == organizationalUnitId.Value);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();
            var pageSize = 20;

            var viewModel = new UserListViewModel
            {
                Users = await query
                    .OrderBy(u => u.FullNameAr)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(),
                SearchTerm = searchTerm,
                RoleId = roleId,
                OrganizationalUnitId = organizationalUnitId,
                IsActive = isActive,
                Roles = new SelectList(await _db.Roles.ToListAsync(), "Id", "NameAr"),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        // GET: /Admin/UserCreate
        public async Task<IActionResult> UserCreate()
        {
            if (!IsAdminUser())
                return Forbid();

            var viewModel = new UserFormViewModel();
            await PopulateUserDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Admin/UserCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserCreate(UserFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (await _db.Users.AnyAsync(u => u.ADUsername == model.ADUsername))
            {
                ModelState.AddModelError("ADUsername", "اسم المستخدم موجود بالفعل");
            }

            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("Password", "كلمة المرور مطلوبة للمستخدم الجديد");
            }

            if (ModelState.IsValid)
            {
                var entity = new User
                {
                    CreatedAt = DateTime.Now,
                    CreatedBy = GetCurrentUserId()  // إضافة من أنشأ المستخدم
                };
                model.UpdateEntity(entity);

                // حفظ كلمة المرور مباشرة (النظام الحالي لا يستخدم تشفير)
                if (!string.IsNullOrEmpty(model.Password))
                {
                    entity.PasswordHash = model.Password;
                }

                _db.Users.Add(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المستخدم بنجاح";
                return RedirectToAction(nameof(Users));
            }

            await PopulateUserDropdowns(model);
            return View(model);
        }

        // GET: /Admin/UserEdit/5
        public async Task<IActionResult> UserEdit(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return NotFound();

            var viewModel = UserFormViewModel.FromEntity(entity);
            await PopulateUserDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Admin/UserEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserEdit(int id, UserFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (id != model.Id)
                return NotFound();

            if (await _db.Users.AnyAsync(u => u.ADUsername == model.ADUsername && u.Id != id))
            {
                ModelState.AddModelError("ADUsername", "اسم المستخدم موجود بالفعل");
            }

            // Password is optional for edit
            if (!string.IsNullOrEmpty(model.Password) && model.Password.Length < 6)
            {
                ModelState.AddModelError("Password", "كلمة المرور يجب أن تكون 6 أحرف على الأقل");
            }

            if (ModelState.IsValid)
            {
                var entity = await _db.Users.FindAsync(id);
                if (entity == null)
                    return NotFound();

                model.UpdateEntity(entity);

                // تحديث كلمة المرور فقط إذا تم إدخالها
                if (!string.IsNullOrEmpty(model.Password))
                {
                    entity.PasswordHash = model.Password;  // بدون تشفير
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث المستخدم بنجاح";
                return RedirectToAction(nameof(Users));
            }

            await PopulateUserDropdowns(model);
            return View(model);
        }

        // POST: /Admin/UserDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserDelete(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return NotFound();

            // Prevent deleting current user
            if (entity.Id == GetCurrentUserId())
            {
                TempData["ErrorMessage"] = "لا يمكنك حذف حسابك الخاص";
                return RedirectToAction(nameof(Users));
            }

            // Check for dependencies
            if (await _db.Initiatives.AnyAsync(i => i.SupervisorId == id || i.CreatedById == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف المستخدم لوجود مبادرات مرتبطة به";
                return RedirectToAction(nameof(Users));
            }

            _db.Users.Remove(entity);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المستخدم بنجاح";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/UserToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserToggleActive(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return NotFound();

            entity.IsActive = !entity.IsActive;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = entity.IsActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم";
            return RedirectToAction(nameof(Users));
        }

        private async Task PopulateUserDropdowns(UserFormViewModel model)
        {
            model.Roles = new SelectList(
                await _db.Roles.ToListAsync(),
                "Id", "NameAr", model.RoleId);

            model.OrganizationalUnits = new SelectList(
                "Id", "NameAr", model.OrganizationalUnitId);

                "Id", "NameAr", model.OrganizationId);
        }

        #endregion

        #region FiscalYears

        // GET: /Admin/FiscalYears
        public async Task<IActionResult> FiscalYears(int? organizationId)
        {
            if (!IsAdminUser())
                return Forbid();

            var query = _db.FiscalYears
                .AsQueryable();

            if (organizationId.HasValue)
            {
            }

            var viewModel = new FiscalYearListViewModel
            {
                FiscalYears = await query.OrderByDescending(f => f.Year).ToListAsync(),
                TotalCount = await query.CountAsync()
            };

            return View(viewModel);
        }

        // GET: /Admin/FiscalYearCreate
        public async Task<IActionResult> FiscalYearCreate()
        {
            if (!IsAdminUser())
                return Forbid();

            var viewModel = new FiscalYearFormViewModel
            {
                Year = DateTime.Now.Year,
                NameAr = $"السنة المالية {DateTime.Now.Year}",
                NameEn = $"Fiscal Year {DateTime.Now.Year}",
                StartDate = new DateTime(DateTime.Now.Year, 1, 1),
                EndDate = new DateTime(DateTime.Now.Year, 12, 31)
            };

                "Id", "NameAr");

            return View(viewModel);
        }

        // POST: /Admin/FiscalYearCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearCreate(FiscalYearFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            {
                ModelState.AddModelError("Year", "هذه السنة المالية موجودة بالفعل لهذه المنظمة");
            }

            if (ModelState.IsValid)
            {
                var entity = new FiscalYear
                {
                    CreatedAt = DateTime.Now,
                    CreatedBy = GetCurrentUserId()
                };
                model.UpdateEntity(entity);

                // If this is current, reset others
                if (entity.IsCurrent)
                {
                    await _db.FiscalYears
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));
                }

                _db.FiscalYears.Add(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة السنة المالية بنجاح";
                return RedirectToAction(nameof(FiscalYears));
            }

                "Id", "NameAr", model.OrganizationId);

            return View(model);
        }

        // GET: /Admin/FiscalYearEdit/5
        public async Task<IActionResult> FiscalYearEdit(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null)
                return NotFound();

            var viewModel = FiscalYearFormViewModel.FromEntity(entity);
                "Id", "NameAr", viewModel.OrganizationId);

            return View(viewModel);
        }

        // POST: /Admin/FiscalYearEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearEdit(int id, FiscalYearFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (id != model.Id)
                return NotFound();

            {
                ModelState.AddModelError("Year", "هذه السنة المالية موجودة بالفعل لهذه المنظمة");
            }

            if (ModelState.IsValid)
            {
                var entity = await _db.FiscalYears.FindAsync(id);
                if (entity == null)
                    return NotFound();

                // If setting as current, reset others
                if (model.IsCurrent && !entity.IsCurrent)
                {
                    await _db.FiscalYears
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));
                }

                model.UpdateEntity(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث السنة المالية بنجاح";
                return RedirectToAction(nameof(FiscalYears));
            }

                "Id", "NameAr", model.OrganizationId);

            return View(model);
        }

        // POST: /Admin/FiscalYearDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearDelete(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null)
                return NotFound();

            // Check for dependencies
            if (await _db.Initiatives.AnyAsync(i => i.FiscalYearId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف السنة المالية لوجود مبادرات مرتبطة بها";
                return RedirectToAction(nameof(FiscalYears));
            }

            _db.FiscalYears.Remove(entity);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف السنة المالية بنجاح";
            return RedirectToAction(nameof(FiscalYears));
        }

        // POST: /Admin/FiscalYearSetCurrent/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearSetCurrent(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null)
                return NotFound();

            // Reset all others for same organization
            await _db.FiscalYears
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));

            entity.IsCurrent = true;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعيين السنة المالية الحالية";
            return RedirectToAction(nameof(FiscalYears));
        }

        #endregion

        #region Roles

        // GET: /Admin/Roles
        public async Task<IActionResult> Roles()
        {
            if (!IsAdminUser())
                return Forbid();

            var viewModel = new RoleListViewModel
            {
                Roles = await _db.Roles.ToListAsync(),
                TotalCount = await _db.Roles.CountAsync()
            };

            return View(viewModel);
        }

        // GET: /Admin/RoleEdit/5
        public async Task<IActionResult> RoleEdit(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.Roles.FindAsync(id);
            if (entity == null)
                return NotFound();

            return View(RoleFormViewModel.FromEntity(entity));
        }

        // POST: /Admin/RoleEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleEdit(int id, RoleFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _db.Roles.FindAsync(id);
                if (entity == null)
                    return NotFound();

                model.UpdateEntity(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث الدور بنجاح";
                return RedirectToAction(nameof(Roles));
            }

            return View(model);
        }

        #endregion

        #region SupportingEntities - جهات المساندة

        // GET: /Admin/SupportingEntities
        public async Task<IActionResult> SupportingEntities(string? searchTerm, int? organizationId, bool? isActive)
        {
            if (!IsAdminUser())
                return Forbid();

            var query = _db.SupportingEntities
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => e.NameAr.Contains(searchTerm) ||
                                        e.NameEn.Contains(searchTerm) ||
                                        e.Code.Contains(searchTerm));
            }

            if (organizationId.HasValue)
            {
            }

            if (isActive.HasValue)
            {
                query = query.Where(e => e.IsActive == isActive.Value);
            }

            var viewModel = new SupportingEntityListViewModel
            {
                Entities = await query.OrderBy(e => e.NameAr).ToListAsync(),
                SearchTerm = searchTerm,
                IsActive = isActive,
                TotalCount = await query.CountAsync()
            };

            return View(viewModel);
        }

        // GET: /Admin/SupportingEntityCreate
        public async Task<IActionResult> SupportingEntityCreate()
        {
            if (!IsAdminUser())
                return Forbid();

            var viewModel = new SupportingEntityFormViewModel();
            await PopulateSupportingEntityDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Admin/SupportingEntityCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityCreate(SupportingEntityFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (ModelState.IsValid)
            {
                // إنشاء الكود تلقائياً: SE-{رقم المنظمة}-{رقم تسلسلي}
                var entitiesInOrg = await _db.SupportingEntities
                    .CountAsync();
                int nextNumber = entitiesInOrg + 1;
                string autoCode = $"SE-{model.OrganizationId:D3}-{nextNumber:D3}";

                var entity = new SupportingEntity
                {
                    Code = autoCode,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    OrderIndex = nextNumber,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now,
                    CreatedById = GetCurrentUserId()
                };

                _db.SupportingEntities.Add(entity);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"تم إضافة جهة المساندة بنجاح (الكود: {autoCode})";
                return RedirectToAction(nameof(SupportingEntities));
            }

            await PopulateSupportingEntityDropdowns(model);
            return View(model);
        }

        // GET: /Admin/SupportingEntityEdit/5
        public async Task<IActionResult> SupportingEntityEdit(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.SupportingEntities
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null)
                return NotFound();

            var viewModel = SupportingEntityFormViewModel.FromEntity(entity);
            await PopulateSupportingEntityDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Admin/SupportingEntityEdit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityEdit(int id, SupportingEntityFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _db.SupportingEntities.FindAsync(id);
                if (entity == null)
                    return NotFound();

                // تحديث البيانات (بدون تغيير الكود أو المنظمة)
                entity.NameAr = model.NameAr;
                entity.NameEn = model.NameEn;
                entity.IsActive = model.IsActive;
                entity.LastModifiedById = GetCurrentUserId();
                entity.LastModifiedAt = DateTime.Now;

                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تحديث جهة المساندة بنجاح";
                return RedirectToAction(nameof(SupportingEntities));
            }

            await PopulateSupportingEntityDropdowns(model);
            return View(model);
        }

        // POST: /Admin/SupportingEntityDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityDelete(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null)
                return NotFound();

            // التحقق من عدم وجود مشاريع مرتبطة
            if (await _db.ProjectSupportingUnits.AnyAsync(p => p.SupportingEntityId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف جهة المساندة لوجود مشاريع مرتبطة بها";
                return RedirectToAction(nameof(SupportingEntities));
            }

            _db.SupportingEntities.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف جهة المساندة بنجاح";
            return RedirectToAction(nameof(SupportingEntities));
        }

        // POST: /Admin/SupportingEntityToggle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityToggle(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null)
                return NotFound();

            entity.IsActive = !entity.IsActive;
            entity.LastModifiedById = GetCurrentUserId();
            entity.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = entity.IsActive ? "تم تفعيل جهة المساندة" : "تم تعطيل جهة المساندة";
            return RedirectToAction(nameof(SupportingEntities));
        }

        private async Task PopulateSupportingEntityDropdowns(SupportingEntityFormViewModel model)
        {
                "Id", "NameAr", model.OrganizationId);
        }

        #endregion


        // ========================================
        // تعديلات على AdminController - قسم Vision & Mission Settings
        // استبدل القسم الحالي بهذا الكود
        // ========================================

        #region Vision & Mission Settings

        // GET: /Admin/VisionMission
        public async Task<IActionResult> VisionMission()
        {
            if (!IsAdminUser())
                return Forbid();

            // جلب إعدادات النظام (أو إنشاء سجل جديد إذا لم يوجد)
            var systemSettings = await _db.SystemSettings
                .Include(s => s.LastModifiedBy)
                .FirstOrDefaultAsync();

            // جلب إعدادات الوحدات
            var unitSettings = await _db.OrganizationalUnitSettings
                .Include(u => u.CreatedBy)
                .OrderBy(u => u.ExternalUnitName ?? u.OrganizationalUnit.NameAr)
                .ToListAsync();

            var viewModel = new VisionMissionViewModel
            {
                SystemSettings = SystemSettingsViewModel.FromEntity(systemSettings),
                UnitSettings = unitSettings
                // لم نعد نحتاج AvailableUnits لأننا نستخدم API
            };

            return View(viewModel);
        }

        // POST: /Admin/SaveSystemSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSystemSettings(VisionMissionViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var systemSettings = await _db.SystemSettings.FirstOrDefaultAsync();

            if (systemSettings == null)
            {
                // إنشاء سجل جديد
                systemSettings = new SystemSettings();
                _db.SystemSettings.Add(systemSettings);
            }

            model.SystemSettings.UpdateEntity(systemSettings);
            systemSettings.LastModifiedById = GetCurrentUserId();
            systemSettings.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حفظ إعدادات النظام بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        // POST: /Admin/AddUnitSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnitSettings(
            int? ExternalUnitId,
            string? ExternalUnitName,
            string? VisionAr,
            string? VisionEn,
            string? MissionAr,
            string? MissionEn)
        {
            if (!IsAdminUser())
                return Forbid();

            // التحقق من عدم وجود إعدادات سابقة لهذه الوحدة
            if (ExternalUnitId.HasValue)
            {
                var exists = await _db.OrganizationalUnitSettings
                    .AnyAsync(u => u.ExternalUnitId == ExternalUnitId);

                if (exists)
                {
                    TempData["ErrorMessage"] = "هذه الوحدة لديها إعدادات مسبقة";
                    return RedirectToAction(nameof(VisionMission));
                }
            }

            var unitSettings = new OrganizationalUnitSettings
            {
                // الحقول الجديدة من API
                ExternalUnitId = ExternalUnitId,
                ExternalUnitName = ExternalUnitName,
                // الحقل القديم - null عند استخدام API
                OrganizationalUnitId = null,
                VisionAr = VisionAr,
                VisionEn = VisionEn,
                MissionAr = MissionAr,
                MissionEn = MissionEn,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.OrganizationalUnitSettings.Add(unitSettings);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        // GET: /Admin/EditUnitSettings/5
        public async Task<IActionResult> EditUnitSettings(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var unitSettings = await _db.OrganizationalUnitSettings
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unitSettings == null)
                return NotFound();

            var viewModel = UnitSettingsFormViewModel.FromEntity(unitSettings);

            return View(viewModel);
        }

        // POST: /Admin/EditUnitSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnitSettings(UnitSettingsFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(model.Id);

            if (unitSettings == null)
                return NotFound();

            model.UpdateEntity(unitSettings);
            unitSettings.LastModifiedById = GetCurrentUserId();
            unitSettings.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        // POST: /Admin/DeleteUnitSettings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnitSettings(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(id);

            if (unitSettings == null)
                return NotFound();

            _db.OrganizationalUnitSettings.Remove(unitSettings);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        #endregion


        #region Strategic Planning

        // GET: /Admin/StrategicPlanning
        public async Task<IActionResult> StrategicPlanning()
        {
            if (!IsAdminUser())
                return Forbid();

            var viewModel = new StrategicPlanningViewModel
            {
                Axes = await _db.StrategicAxes
                    .Include(a => a.StrategicObjectives)
                    .OrderBy(a => a.OrderIndex)
                    .ToListAsync(),

                StrategicObjectives = await _db.StrategicObjectives
                    .Include(s => s.StrategicAxis)
                    .Include(s => s.MainObjectives)
                    .OrderBy(s => s.StrategicAxis.OrderIndex)
                    .ThenBy(s => s.OrderIndex)
                    .ToListAsync(),

                MainObjectives = await _db.MainObjectives
                    .Include(m => m.StrategicObjective)
                        .ThenInclude(s => s.StrategicAxis)
                    .Include(m => m.SubObjectives)
                    .OrderBy(m => m.StrategicObjective.OrderIndex)
                    .ThenBy(m => m.OrderIndex)
                    .ToListAsync(),

                SubObjectives = await _db.SubObjectives
                    .Include(s => s.MainObjective)
                        .ThenInclude(m => m.StrategicObjective)
                            .ThenInclude(so => so.StrategicAxis)
                    .OrderBy(s => s.MainObjective.OrderIndex)
                    .ThenBy(s => s.OrderIndex)
                    .ToListAsync(),

                CoreValues = await _db.CoreValues
                    .OrderBy(v => v.OrderIndex)
                    .ToListAsync(),

                OrganizationalUnitsDropdown = new SelectList(
                    await _db.ExternalOrganizationalUnits
                        .Where(u => u.IsActive)
                        .OrderBy(u => u.ArabicName)
                        .ThenBy(u => u.NameAr)
                        .Select(u => new { u.Id, Name = u.ArabicName })
                        .ToListAsync(),
                    "Id", "Name")
            };

            return View(viewModel);
        }

        #region Axes

        // POST: /Admin/AddAxis
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAxis(string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var lastAxis = await _db.StrategicAxes.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            int nextNumber = (lastAxis?.Id ?? 0) + 1;
            string code = $"AX-{nextNumber:D2}";

            var axis = new StrategicAxis
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.StrategicAxes.Add(axis);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة المحور بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // GET: /Admin/EditAxis/5
        public async Task<IActionResult> EditAxis(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var axis = await _db.StrategicAxes.FindAsync(id);
            if (axis == null)
                return NotFound();

            return View(StrategicAxisFormViewModel.FromEntity(axis));
        }

        // POST: /Admin/EditAxis
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAxis(StrategicAxisFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var axis = await _db.StrategicAxes.FindAsync(model.Id);
            if (axis == null)
                return NotFound();

            model.UpdateEntity(axis);
            axis.LastModifiedById = GetCurrentUserId();
            axis.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث المحور بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // POST: /Admin/DeleteAxis/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAxis(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var axis = await _db.StrategicAxes
                .Include(a => a.StrategicObjectives)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (axis == null)
                return NotFound();

            if (axis.StrategicObjectives.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف المحور لوجود أهداف استراتيجية مرتبطة به";
                return RedirectToAction(nameof(StrategicPlanning));
            }

            _db.StrategicAxes.Remove(axis);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف المحور بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #region Strategic Objectives

        // POST: /Admin/AddStrategicObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStrategicObjective(int StrategicAxisId, string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var lastObj = await _db.StrategicObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            int nextNumber = (lastObj?.Id ?? 0) + 1;
            string code = $"SO-{nextNumber:D2}";

            var objective = new StrategicObjective
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                StrategicAxisId = StrategicAxisId,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.StrategicObjectives.Add(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // GET: /Admin/EditStrategicObjective/5
        public async Task<IActionResult> EditStrategicObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.StrategicObjectives.FindAsync(id);
            if (objective == null)
                return NotFound();

            var viewModel = StrategicObjectiveFormViewModel.FromEntity(objective);
            viewModel.Axes = new SelectList(
                await _db.StrategicAxes.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToListAsync(),
                "Id", "NameAr", objective.StrategicAxisId);

            return View(viewModel);
        }

        // POST: /Admin/EditStrategicObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStrategicObjective(StrategicObjectiveFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.StrategicObjectives.FindAsync(model.Id);
            if (objective == null)
                return NotFound();

            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // POST: /Admin/DeleteStrategicObjective/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStrategicObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.StrategicObjectives
                .Include(o => o.MainObjectives)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (objective == null)
                return NotFound();

            if (objective.MainObjectives.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الهدف الاستراتيجي لوجود أهداف رئيسية مرتبطة به";
                return RedirectToAction(nameof(StrategicPlanning));
            }

            _db.StrategicObjectives.Remove(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #region Main Objectives

        // POST: /Admin/AddMainObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMainObjective(int StrategicObjectiveId, string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var lastObj = await _db.MainObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            int nextNumber = (lastObj?.Id ?? 0) + 1;
            string code = $"MO-{nextNumber:D2}";

            var objective = new MainObjective
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                StrategicObjectiveId = StrategicObjectiveId,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.MainObjectives.Add(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // GET: /Admin/EditMainObjective/5
        public async Task<IActionResult> EditMainObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.MainObjectives.FindAsync(id);
            if (objective == null)
                return NotFound();

            var viewModel = MainObjectiveFormViewModel.FromEntity(objective);
            viewModel.StrategicObjectives = new SelectList(
                await _db.StrategicObjectives
                    .Include(s => s.StrategicAxis)
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.StrategicAxis.OrderIndex)
                    .ThenBy(s => s.OrderIndex)
                    .Select(s => new { s.Id, Name = s.StrategicAxis.NameAr + " - " + s.NameAr })
                    .ToListAsync(),
                "Id", "Name", objective.StrategicObjectiveId);

            return View(viewModel);
        }

        // POST: /Admin/EditMainObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMainObjective(MainObjectiveFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.MainObjectives.FindAsync(model.Id);
            if (objective == null)
                return NotFound();

            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // POST: /Admin/DeleteMainObjective/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMainObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.MainObjectives
                .Include(o => o.SubObjectives)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (objective == null)
                return NotFound();

            if (objective.SubObjectives.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الهدف لوجود أهداف فرعية مرتبطة به";
                return RedirectToAction(nameof(StrategicPlanning));
            }

            _db.MainObjectives.Remove(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

      

        #region Sub Objectives

        // POST: /Admin/AddSubObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubObjective(
            int MainObjectiveId,
            int? ExternalUnitId,
            string? ExternalUnitName,
            string NameAr,
            string NameEn,
            string? DescriptionAr,
            int OrderIndex,
            bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var lastObj = await _db.SubObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            int nextNumber = (lastObj?.Id ?? 0) + 1;
            string code = $"SUB-{nextNumber:D2}";

            var objective = new SubObjective
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                MainObjectiveId = MainObjectiveId,
                // الحقول الجديدة من API
                ExternalUnitId = ExternalUnitId,
                ExternalUnitName = ExternalUnitName,
                // الحقل القديم - null إذا استخدمنا API
                OrganizationalUnitId = ExternalUnitId.HasValue ? null : 1,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.SubObjectives.Add(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // GET: /Admin/EditSubObjective/5
        public async Task<IActionResult> EditSubObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.SubObjectives
                .Include(s => s.MainObjective)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (objective == null)
                return NotFound();

            var viewModel = SubObjectiveFormViewModel.FromEntity(objective);

            viewModel.MainObjectives = new SelectList(
                await _db.MainObjectives
                    .Include(m => m.StrategicObjective)
                        .ThenInclude(s => s.StrategicAxis)
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.StrategicObjective.OrderIndex)
                    .ThenBy(m => m.OrderIndex)
                    .Select(m => new { m.Id, Name = m.StrategicObjective.StrategicAxis.NameAr + " > " + m.StrategicObjective.NameAr + " > " + m.NameAr })
                    .ToListAsync(),
                "Id", "Name", objective.MainObjectiveId);

            // لم نعد نحتاج OrganizationalUnits dropdown لأننا نستخدم API
            // viewModel.OrganizationalUnits = ...

            return View(viewModel);
        }

        // POST: /Admin/EditSubObjective
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubObjective(SubObjectiveFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.SubObjectives.FindAsync(model.Id);
            if (objective == null)
                return NotFound();

            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // POST: /Admin/DeleteSubObjective/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubObjective(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var objective = await _db.SubObjectives.FindAsync(id);
            if (objective == null)
                return NotFound();

            _db.SubObjectives.Remove(objective);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #region Core Values

        // POST: /Admin/AddValue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddValue(string NameAr, string NameEn, string? MeaningAr, string? Icon, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var value = new CoreValue
            {
                NameAr = NameAr,
                NameEn = NameEn,
                MeaningAr = MeaningAr,
                Icon = Icon,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.CoreValues.Add(value);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // GET: /Admin/EditValue/5
        public async Task<IActionResult> EditValue(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var value = await _db.CoreValues.FindAsync(id);
            if (value == null)
                return NotFound();

            return View(CoreValueFormViewModel.FromEntity(value));
        }

        // POST: /Admin/EditValue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditValue(CoreValueFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var value = await _db.CoreValues.FindAsync(model.Id);
            if (value == null)
                return NotFound();

            model.UpdateEntity(value);
            value.LastModifiedById = GetCurrentUserId();
            value.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        // POST: /Admin/DeleteValue/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteValue(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var value = await _db.CoreValues.FindAsync(id);
            if (value == null)
                return NotFound();

            _db.CoreValues.Remove(value);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #endregion



        #region Financial Costs

        // GET: /Admin/FinancialCosts
        public async Task<IActionResult> FinancialCosts()
        {
            if (!IsAdminUser())
                return Forbid();

            var costs = await _db.FinancialCosts
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            return View(costs);
        }

        // POST: /Admin/AddFinancialCost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFinancialCost(string NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser())
                return Forbid();

            var cost = new FinancialCost
            {
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                DescriptionEn = DescriptionEn,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.FinancialCosts.Add(cost);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        // GET: /Admin/EditFinancialCost/5
        public async Task<IActionResult> EditFinancialCost(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null)
                return NotFound();

            return View(FinancialCostFormViewModel.FromEntity(cost));
        }

        // POST: /Admin/EditFinancialCost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFinancialCost(FinancialCostFormViewModel model)
        {
            if (!IsAdminUser())
                return Forbid();

            var cost = await _db.FinancialCosts.FindAsync(model.Id);
            if (cost == null)
                return NotFound();

            model.UpdateEntity(cost);
            cost.LastModifiedById = GetCurrentUserId();
            cost.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        // POST: /Admin/ToggleFinancialCost/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFinancialCost(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null)
                return NotFound();

            cost.IsActive = !cost.IsActive;
            cost.LastModifiedById = GetCurrentUserId();
            cost.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = cost.IsActive ? "تم تفعيل التكلفة" : "تم تعطيل التكلفة";
            return RedirectToAction(nameof(FinancialCosts));
        }

        // POST: /Admin/DeleteFinancialCost/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFinancialCost(int id)
        {
            if (!IsAdminUser())
                return Forbid();

            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null)
                return NotFound();

            _db.FinancialCosts.Remove(cost);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        #endregion


        /// <summary>
        /// صفحة مزامنة البيانات الخارجية
        /// </summary>
        [HttpGet]
        public IActionResult ExternalSync()
        {
            ViewBag.ApiBaseUrl = _configuration["ExternalApi:BaseUrl"] ?? "غير محدد";
            ViewBag.ApiTenantId = _configuration["ExternalApi:TenantId"] ?? "1";
            return View();
        }

    }
}