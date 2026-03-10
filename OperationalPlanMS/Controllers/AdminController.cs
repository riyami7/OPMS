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

        private bool IsAdminUser() => GetCurrentUserRole() == UserRole.Admin;

        public IActionResult Index()
        {
            if (!IsAdminUser()) return Forbid();
            return View();
        }

        #region Users

        public async Task<IActionResult> Users(string? searchTerm, int? roleId, bool? isActive, int page = 1)
        {
            if (!IsAdminUser()) return Forbid();

            var query = _db.Users.Include(u => u.Role).AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(u => u.FullNameAr.Contains(searchTerm) ||
                                        u.FullNameEn.Contains(searchTerm) ||
                                        u.ADUsername.Contains(searchTerm) ||
                                        u.Email!.Contains(searchTerm));

            if (roleId.HasValue)
                query = query.Where(u => u.RoleId == roleId.Value);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();
            var pageSize = 20;

            var viewModel = new UserListViewModel
            {
                Users = await query.OrderBy(u => u.FullNameAr)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
                SearchTerm = searchTerm,
                RoleId = roleId,
                IsActive = isActive,
                Roles = new SelectList(await _db.Roles.ToListAsync(), "Id", "NameAr"),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        public async Task<IActionResult> UserCreate()
        {
            if (!IsAdminUser()) return Forbid();
            var viewModel = new UserFormViewModel();
            await PopulateUserDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UserCreate(UserFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();

            if (await _db.Users.AnyAsync(u => u.ADUsername == model.ADUsername))
                ModelState.AddModelError("ADUsername", "اسم المستخدم موجود بالفعل");

            if (string.IsNullOrEmpty(model.Password))
                ModelState.AddModelError("Password", "كلمة المرور مطلوبة للمستخدم الجديد");

            if (ModelState.IsValid)
            {
                var entity = new User { CreatedAt = DateTime.Now, CreatedBy = GetCurrentUserId() };
                model.UpdateEntity(entity);
                if (!string.IsNullOrEmpty(model.Password))
                    entity.PasswordHash = model.Password;
                _db.Users.Add(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المستخدم بنجاح";
                return RedirectToAction(nameof(Users));
            }

            await PopulateUserDropdowns(model);
            return View(model);
        }

        public async Task<IActionResult> UserEdit(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.Users.FindAsync(id);
            if (entity == null) return NotFound();
            var viewModel = UserFormViewModel.FromEntity(entity);
            await PopulateUserDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UserEdit(int id, UserFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            if (id != model.Id) return NotFound();

            if (await _db.Users.AnyAsync(u => u.ADUsername == model.ADUsername && u.Id != id))
                ModelState.AddModelError("ADUsername", "اسم المستخدم موجود بالفعل");

            if (!string.IsNullOrEmpty(model.Password) && model.Password.Length < 6)
                ModelState.AddModelError("Password", "كلمة المرور يجب أن تكون 6 أحرف على الأقل");

            if (ModelState.IsValid)
            {
                var entity = await _db.Users.FindAsync(id);
                if (entity == null) return NotFound();
                model.UpdateEntity(entity);
                if (!string.IsNullOrEmpty(model.Password))
                    entity.PasswordHash = model.Password;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث المستخدم بنجاح";
                return RedirectToAction(nameof(Users));
            }

            await PopulateUserDropdowns(model);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UserDelete(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.Users.FindAsync(id);
            if (entity == null) return NotFound();

            if (entity.Id == GetCurrentUserId())
            {
                TempData["ErrorMessage"] = "لا يمكنك حذف حسابك الخاص";
                return RedirectToAction(nameof(Users));
            }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UserToggleActive(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.Users.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = !entity.IsActive;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = entity.IsActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم";
            return RedirectToAction(nameof(Users));
        }

        private async Task PopulateUserDropdowns(UserFormViewModel model)
        {
            model.Roles = new SelectList(await _db.Roles.ToListAsync(), "Id", "NameAr", model.RoleId);
        }

        #endregion

        #region FiscalYears

        public async Task<IActionResult> FiscalYears()
        {
            if (!IsAdminUser()) return Forbid();

            var viewModel = new FiscalYearListViewModel
            {
                FiscalYears = await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                TotalCount = await _db.FiscalYears.CountAsync()
            };

            return View(viewModel);
        }

        public IActionResult FiscalYearCreate()
        {
            if (!IsAdminUser()) return Forbid();

            var viewModel = new FiscalYearFormViewModel
            {
                Year = DateTime.Now.Year,
                NameAr = $"السنة المالية {DateTime.Now.Year}",
                NameEn = $"Fiscal Year {DateTime.Now.Year}",
                StartDate = new DateTime(DateTime.Now.Year, 1, 1),
                EndDate = new DateTime(DateTime.Now.Year, 12, 31)
            };

            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearCreate(FiscalYearFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();

            if (await _db.FiscalYears.AnyAsync(f => f.Year == model.Year))
                ModelState.AddModelError("Year", "هذه السنة المالية موجودة بالفعل");

            if (ModelState.IsValid)
            {
                var entity = new FiscalYear { CreatedAt = DateTime.Now, CreatedBy = GetCurrentUserId() };
                model.UpdateEntity(entity);

                if (entity.IsCurrent)
                    await _db.FiscalYears.Where(f => f.IsCurrent)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));

                _db.FiscalYears.Add(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة السنة المالية بنجاح";
                return RedirectToAction(nameof(FiscalYears));
            }

            return View(model);
        }

        public async Task<IActionResult> FiscalYearEdit(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();
            return View(FiscalYearFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearEdit(int id, FiscalYearFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            if (id != model.Id) return NotFound();

            if (await _db.FiscalYears.AnyAsync(f => f.Year == model.Year && f.Id != id))
                ModelState.AddModelError("Year", "هذه السنة المالية موجودة بالفعل");

            if (ModelState.IsValid)
            {
                var entity = await _db.FiscalYears.FindAsync(id);
                if (entity == null) return NotFound();

                if (model.IsCurrent && !entity.IsCurrent)
                    await _db.FiscalYears.Where(f => f.IsCurrent)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));

                model.UpdateEntity(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث السنة المالية بنجاح";
                return RedirectToAction(nameof(FiscalYears));
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearDelete(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FiscalYearSetCurrent(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();

            await _db.FiscalYears.Where(f => f.IsCurrent)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));

            entity.IsCurrent = true;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تعيين السنة المالية الحالية";
            return RedirectToAction(nameof(FiscalYears));
        }

        #endregion

        #region Roles

        public async Task<IActionResult> Roles()
        {
            if (!IsAdminUser()) return Forbid();
            var viewModel = new RoleListViewModel
            {
                Roles = await _db.Roles.ToListAsync(),
                TotalCount = await _db.Roles.CountAsync()
            };
            return View(viewModel);
        }

        public async Task<IActionResult> RoleEdit(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.Roles.FindAsync(id);
            if (entity == null) return NotFound();
            return View(RoleFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleEdit(int id, RoleFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _db.Roles.FindAsync(id);
                if (entity == null) return NotFound();
                model.UpdateEntity(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث الدور بنجاح";
                return RedirectToAction(nameof(Roles));
            }

            return View(model);
        }

        #endregion

        #region SupportingEntities

        public async Task<IActionResult> SupportingEntities(string? searchTerm, bool? isActive)
        {
            if (!IsAdminUser()) return Forbid();

            var query = _db.SupportingEntities.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(e => e.NameAr.Contains(searchTerm) ||
                                        e.NameEn.Contains(searchTerm) ||
                                        e.Code.Contains(searchTerm));

            if (isActive.HasValue)
                query = query.Where(e => e.IsActive == isActive.Value);

            var viewModel = new SupportingEntityListViewModel
            {
                Entities = await query.OrderBy(e => e.NameAr).ToListAsync(),
                SearchTerm = searchTerm,
                IsActive = isActive,
                TotalCount = await query.CountAsync()
            };

            return View(viewModel);
        }

        public IActionResult SupportingEntityCreate()
        {
            if (!IsAdminUser()) return Forbid();
            return View(new SupportingEntityFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityCreate(SupportingEntityFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();

            if (ModelState.IsValid)
            {
                var count = await _db.SupportingEntities.CountAsync();
                string autoCode = $"SE-{count + 1:D4}";

                var entity = new SupportingEntity
                {
                    Code = autoCode,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    OrderIndex = count + 1,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now,
                    CreatedById = GetCurrentUserId()
                };

                _db.SupportingEntities.Add(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم إضافة جهة المساندة بنجاح (الكود: {autoCode})";
                return RedirectToAction(nameof(SupportingEntities));
            }

            return View(model);
        }

        public async Task<IActionResult> SupportingEntityEdit(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();
            return View(SupportingEntityFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityEdit(int id, SupportingEntityFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _db.SupportingEntities.FindAsync(id);
                if (entity == null) return NotFound();
                entity.NameAr = model.NameAr;
                entity.NameEn = model.NameEn;
                entity.IsActive = model.IsActive;
                entity.LastModifiedById = GetCurrentUserId();
                entity.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث جهة المساندة بنجاح";
                return RedirectToAction(nameof(SupportingEntities));
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityDelete(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportingEntityToggle(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = !entity.IsActive;
            entity.LastModifiedById = GetCurrentUserId();
            entity.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = entity.IsActive ? "تم تفعيل جهة المساندة" : "تم تعطيل جهة المساندة";
            return RedirectToAction(nameof(SupportingEntities));
        }

        #endregion

        #region Vision & Mission Settings

        public async Task<IActionResult> VisionMission()
        {
            if (!IsAdminUser()) return Forbid();

            var systemSettings = await _db.SystemSettings
                .Include(s => s.LastModifiedBy)
                .FirstOrDefaultAsync();

            var unitSettings = await _db.OrganizationalUnitSettings
                .Include(u => u.CreatedBy)
                .OrderBy(u => u.ExternalUnitName)
                .ToListAsync();

            var viewModel = new VisionMissionViewModel
            {
                SystemSettings = SystemSettingsViewModel.FromEntity(systemSettings),
                UnitSettings = unitSettings
            };

            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSystemSettings(VisionMissionViewModel model)
        {
            if (!IsAdminUser()) return Forbid();

            var systemSettings = await _db.SystemSettings.FirstOrDefaultAsync();
            if (systemSettings == null)
            {
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnitSettings(
            int? ExternalUnitId, string? ExternalUnitName,
            string? VisionAr, string? VisionEn,
            string? MissionAr, string? MissionEn)
        {
            if (!IsAdminUser()) return Forbid();

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
                ExternalUnitId = ExternalUnitId,
                ExternalUnitName = ExternalUnitName,
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

        public async Task<IActionResult> EditUnitSettings(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(id);
            if (unitSettings == null) return NotFound();
            return View(UnitSettingsFormViewModel.FromEntity(unitSettings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnitSettings(UnitSettingsFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(model.Id);
            if (unitSettings == null) return NotFound();
            model.UpdateEntity(unitSettings);
            unitSettings.LastModifiedById = GetCurrentUserId();
            unitSettings.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnitSettings(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(id);
            if (unitSettings == null) return NotFound();
            _db.OrganizationalUnitSettings.Remove(unitSettings);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        #endregion

        #region Strategic Planning

        public async Task<IActionResult> StrategicPlanning()
        {
            if (!IsAdminUser()) return Forbid();

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
                    .ToListAsync()
            };

            return View(viewModel);
        }

        #region Axes

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAxis(string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();

            var lastAxis = await _db.StrategicAxes.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            string code = $"AX-{((lastAxis?.Id ?? 0) + 1):D2}";

            _db.StrategicAxes.Add(new StrategicAxis
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة المحور بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        public async Task<IActionResult> EditAxis(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var axis = await _db.StrategicAxes.FindAsync(id);
            if (axis == null) return NotFound();
            return View(StrategicAxisFormViewModel.FromEntity(axis));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAxis(StrategicAxisFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var axis = await _db.StrategicAxes.FindAsync(model.Id);
            if (axis == null) return NotFound();
            model.UpdateEntity(axis);
            axis.LastModifiedById = GetCurrentUserId();
            axis.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث المحور بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAxis(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var axis = await _db.StrategicAxes.Include(a => a.StrategicObjectives).FirstOrDefaultAsync(a => a.Id == id);
            if (axis == null) return NotFound();
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStrategicObjective(int StrategicAxisId, string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();
            var last = await _db.StrategicObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            string code = $"SO-{((last?.Id ?? 0) + 1):D2}";
            _db.StrategicObjectives.Add(new StrategicObjective
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
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        public async Task<IActionResult> EditStrategicObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.StrategicObjectives.FindAsync(id);
            if (objective == null) return NotFound();
            var viewModel = StrategicObjectiveFormViewModel.FromEntity(objective);
            viewModel.Axes = new SelectList(await _db.StrategicAxes.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToListAsync(), "Id", "NameAr", objective.StrategicAxisId);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStrategicObjective(StrategicObjectiveFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.StrategicObjectives.FindAsync(model.Id);
            if (objective == null) return NotFound();
            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStrategicObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.StrategicObjectives.Include(o => o.MainObjectives).FirstOrDefaultAsync(o => o.Id == id);
            if (objective == null) return NotFound();
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMainObjective(int StrategicObjectiveId, string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();
            var last = await _db.MainObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            string code = $"MO-{((last?.Id ?? 0) + 1):D2}";
            _db.MainObjectives.Add(new MainObjective
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
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        public async Task<IActionResult> EditMainObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.MainObjectives.FindAsync(id);
            if (objective == null) return NotFound();
            var viewModel = MainObjectiveFormViewModel.FromEntity(objective);
            viewModel.StrategicObjectives = new SelectList(
                await _db.StrategicObjectives.Include(s => s.StrategicAxis).Where(s => s.IsActive)
                    .OrderBy(s => s.StrategicAxis.OrderIndex).ThenBy(s => s.OrderIndex)
                    .Select(s => new { s.Id, Name = s.StrategicAxis.NameAr + " - " + s.NameAr }).ToListAsync(),
                "Id", "Name", objective.StrategicObjectiveId);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMainObjective(MainObjectiveFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.MainObjectives.FindAsync(model.Id);
            if (objective == null) return NotFound();
            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMainObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.MainObjectives.Include(o => o.SubObjectives).FirstOrDefaultAsync(o => o.Id == id);
            if (objective == null) return NotFound();
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubObjective(int MainObjectiveId, int? ExternalUnitId, string? ExternalUnitName, string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();
            var last = await _db.SubObjectives.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
            string code = $"SUB-{((last?.Id ?? 0) + 1):D2}";
            _db.SubObjectives.Add(new SubObjective
            {
                Code = code,
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                MainObjectiveId = MainObjectiveId,
                ExternalUnitId = ExternalUnitId,
                ExternalUnitName = ExternalUnitName,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        public async Task<IActionResult> EditSubObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.SubObjectives.Include(s => s.MainObjective).FirstOrDefaultAsync(s => s.Id == id);
            if (objective == null) return NotFound();
            var viewModel = SubObjectiveFormViewModel.FromEntity(objective);
            viewModel.MainObjectives = new SelectList(
                await _db.MainObjectives.Include(m => m.StrategicObjective).ThenInclude(s => s.StrategicAxis)
                    .Where(m => m.IsActive).OrderBy(m => m.StrategicObjective.OrderIndex).ThenBy(m => m.OrderIndex)
                    .Select(m => new { m.Id, Name = m.StrategicObjective.StrategicAxis.NameAr + " > " + m.StrategicObjective.NameAr + " > " + m.NameAr }).ToListAsync(),
                "Id", "Name", objective.MainObjectiveId);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubObjective(SubObjectiveFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.SubObjectives.FindAsync(model.Id);
            if (objective == null) return NotFound();
            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubObjective(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var objective = await _db.SubObjectives.FindAsync(id);
            if (objective == null) return NotFound();
            _db.SubObjectives.Remove(objective);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #region Core Values

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddValue(string NameAr, string NameEn, string? MeaningAr, string? Icon, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();
            _db.CoreValues.Add(new CoreValue
            {
                NameAr = NameAr,
                NameEn = NameEn,
                MeaningAr = MeaningAr,
                Icon = Icon,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        public async Task<IActionResult> EditValue(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var value = await _db.CoreValues.FindAsync(id);
            if (value == null) return NotFound();
            return View(CoreValueFormViewModel.FromEntity(value));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditValue(CoreValueFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var value = await _db.CoreValues.FindAsync(model.Id);
            if (value == null) return NotFound();
            model.UpdateEntity(value);
            value.LastModifiedById = GetCurrentUserId();
            value.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteValue(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var value = await _db.CoreValues.FindAsync(id);
            if (value == null) return NotFound();
            _db.CoreValues.Remove(value);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف القيمة بنجاح";
            return RedirectToAction(nameof(StrategicPlanning));
        }

        #endregion

        #endregion

        #region Financial Costs

        public async Task<IActionResult> FinancialCosts()
        {
            if (!IsAdminUser()) return Forbid();
            return View(await _db.FinancialCosts.OrderBy(c => c.OrderIndex).ToListAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFinancialCost(string NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdminUser()) return Forbid();
            _db.FinancialCosts.Add(new FinancialCost
            {
                NameAr = NameAr,
                NameEn = NameEn,
                DescriptionAr = DescriptionAr,
                DescriptionEn = DescriptionEn,
                OrderIndex = OrderIndex,
                IsActive = IsActive,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        public async Task<IActionResult> EditFinancialCost(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null) return NotFound();
            return View(FinancialCostFormViewModel.FromEntity(cost));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFinancialCost(FinancialCostFormViewModel model)
        {
            if (!IsAdminUser()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(model.Id);
            if (cost == null) return NotFound();
            model.UpdateEntity(cost);
            cost.LastModifiedById = GetCurrentUserId();
            cost.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFinancialCost(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null) return NotFound();
            cost.IsActive = !cost.IsActive;
            cost.LastModifiedById = GetCurrentUserId();
            cost.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = cost.IsActive ? "تم تفعيل التكلفة" : "تم تعطيل التكلفة";
            return RedirectToAction(nameof(FinancialCosts));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFinancialCost(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null) return NotFound();
            _db.FinancialCosts.Remove(cost);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        #endregion

        [HttpGet]
        public IActionResult ExternalSync()
        {
            ViewBag.ApiBaseUrl = _configuration["ExternalApi:BaseUrl"] ?? "غير محدد";
            ViewBag.ApiTenantId = _configuration["ExternalApi:TenantId"] ?? "1";
            return View();
        }
    }
}