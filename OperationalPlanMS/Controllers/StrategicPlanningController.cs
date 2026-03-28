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
    public class StrategicPlanningController : BaseController
    {
        private readonly AppDbContext _db;

        public StrategicPlanningController(AppDbContext db)
        {
            _db = db;
        }

        #region Main Page

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();

            var viewModel = new StrategicPlanningViewModel
            {
                Axes = await _db.StrategicAxes
                    .Include(a => a.StrategicObjectives)
                    .OrderBy(a => a.OrderIndex).ToListAsync(),
                StrategicObjectives = await _db.StrategicObjectives
                    .Include(s => s.StrategicAxis).Include(s => s.MainObjectives)
                    .OrderBy(s => s.StrategicAxis.OrderIndex).ThenBy(s => s.OrderIndex).ToListAsync(),
                MainObjectives = await _db.MainObjectives
                    .Include(m => m.StrategicObjective).ThenInclude(s => s.StrategicAxis)
                    .Include(m => m.SubObjectives)
                    .OrderBy(m => m.StrategicObjective.OrderIndex).ThenBy(m => m.OrderIndex).ToListAsync(),
                SubObjectives = await _db.SubObjectives
                    .Include(s => s.MainObjective).ThenInclude(m => m.StrategicObjective).ThenInclude(so => so.StrategicAxis)
                    .OrderBy(s => s.MainObjective.OrderIndex).ThenBy(s => s.OrderIndex).ToListAsync(),
                CoreValues = await _db.CoreValues.OrderBy(v => v.OrderIndex).ToListAsync()
            };

            return View(viewModel);
        }

        #endregion

        #region Vision & Mission

        public async Task<IActionResult> VisionMission()
        {
            if (!IsAdmin()) return Forbid();

            var viewModel = new VisionMissionViewModel
            {
                SystemSettings = SystemSettingsViewModel.FromEntity(
                    await _db.SystemSettings.Include(s => s.LastModifiedBy).FirstOrDefaultAsync()),
                UnitSettings = await _db.OrganizationalUnitSettings
                    .Include(u => u.CreatedBy).OrderBy(u => u.ExternalUnitName).ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSystemSettings(VisionMissionViewModel model)
        {
            if (!IsAdmin()) return Forbid();

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
            Guid? ExternalUnitId, string? ExternalUnitName,
            string? VisionAr, string? VisionEn, string? MissionAr, string? MissionEn)
        {
            if (!IsAdmin()) return Forbid();

            if (ExternalUnitId.HasValue &&
                await _db.OrganizationalUnitSettings.AnyAsync(u => u.ExternalUnitId == ExternalUnitId))
            {
                TempData["ErrorMessage"] = "هذه الوحدة لديها إعدادات مسبقة";
                return RedirectToAction(nameof(VisionMission));
            }

            _db.OrganizationalUnitSettings.Add(new OrganizationalUnitSettings
            {
                ExternalUnitId = ExternalUnitId,
                ExternalUnitName = ExternalUnitName,
                VisionAr = VisionAr, VisionEn = VisionEn,
                MissionAr = MissionAr, MissionEn = MissionEn,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        public async Task<IActionResult> EditUnitSettings(int id)
        {
            if (!IsAdmin()) return Forbid();
            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(id);
            if (unitSettings == null) return NotFound();
            return View(UnitSettingsFormViewModel.FromEntity(unitSettings));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnitSettings(UnitSettingsFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
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
            if (!IsAdmin()) return Forbid();
            var unitSettings = await _db.OrganizationalUnitSettings.FindAsync(id);
            if (unitSettings == null) return NotFound();
            _db.OrganizationalUnitSettings.Remove(unitSettings);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف إعدادات الوحدة بنجاح";
            return RedirectToAction(nameof(VisionMission));
        }

        #endregion

        #region Axes

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAxis(string NameAr, string NameEn, string? DescriptionAr, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            var lastAxis = await _db.StrategicAxes.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            string code = $"AX-{((lastAxis?.Id ?? 0) + 1):D2}";
            _db.StrategicAxes.Add(new StrategicAxis
            {
                Code = code, NameAr = NameAr, NameEn = NameEn, DescriptionAr = DescriptionAr,
                OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة المحور بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditAxis(int id)
        {
            if (!IsAdmin()) return Forbid();
            var axis = await _db.StrategicAxes.FindAsync(id);
            if (axis == null) return NotFound();
            return View(StrategicAxisFormViewModel.FromEntity(axis));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAxis(StrategicAxisFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            var axis = await _db.StrategicAxes.FindAsync(model.Id);
            if (axis == null) return NotFound();
            model.UpdateEntity(axis);
            axis.LastModifiedById = GetCurrentUserId();
            axis.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث المحور بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAxis(int id)
        {
            if (!IsAdmin()) return Forbid();
            var axis = await _db.StrategicAxes.FindAsync(id);
            if (axis == null) return NotFound();
            if (await _db.StrategicObjectives.AnyAsync(s => s.StrategicAxisId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف المحور لوجود أهداف استراتيجية مرتبطة به";
                return RedirectToAction(nameof(Index));
            }
            _db.StrategicAxes.Remove(axis);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المحور بنجاح";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Strategic Objectives

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStrategicObjective(string NameAr, string NameEn, string? DescriptionAr, int StrategicAxisId, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            var lastObj = await _db.StrategicObjectives.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
            string code = $"SO-{((lastObj?.Id ?? 0) + 1):D2}";
            _db.StrategicObjectives.Add(new StrategicObjective
            {
                Code = code, NameAr = NameAr, NameEn = NameEn, DescriptionAr = DescriptionAr,
                StrategicAxisId = StrategicAxisId, OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditStrategicObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.StrategicObjectives.Include(s => s.StrategicAxis).FirstOrDefaultAsync(s => s.Id == id);
            if (obj == null) return NotFound();
            var viewModel = StrategicObjectiveFormViewModel.FromEntity(obj);
            viewModel.Axes = new SelectList(await _db.StrategicAxes.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToListAsync(), "Id", "NameAr", obj.StrategicAxisId);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStrategicObjective(StrategicObjectiveFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.StrategicObjectives.FindAsync(model.Id);
            if (obj == null) return NotFound();
            model.UpdateEntity(obj);
            obj.LastModifiedById = GetCurrentUserId();
            obj.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStrategicObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.StrategicObjectives.FindAsync(id);
            if (obj == null) return NotFound();
            if (await _db.MainObjectives.AnyAsync(m => m.StrategicObjectiveId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الهدف الاستراتيجي لوجود أهداف رئيسية مرتبطة به";
                return RedirectToAction(nameof(Index));
            }
            _db.StrategicObjectives.Remove(obj);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الهدف الاستراتيجي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Main Objectives

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMainObjective(string NameAr, string NameEn, string? DescriptionAr, int StrategicObjectiveId, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            var lastObj = await _db.MainObjectives.OrderByDescending(m => m.Id).FirstOrDefaultAsync();
            string code = $"MO-{((lastObj?.Id ?? 0) + 1):D2}";
            _db.MainObjectives.Add(new MainObjective
            {
                Code = code, NameAr = NameAr, NameEn = NameEn, DescriptionAr = DescriptionAr,
                StrategicObjectiveId = StrategicObjectiveId, OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditMainObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.MainObjectives.Include(m => m.StrategicObjective).ThenInclude(s => s.StrategicAxis).FirstOrDefaultAsync(m => m.Id == id);
            if (obj == null) return NotFound();
            var viewModel = MainObjectiveFormViewModel.FromEntity(obj);
            viewModel.StrategicObjectives = new SelectList(
                await _db.StrategicObjectives.Include(s => s.StrategicAxis).Where(s => s.IsActive)
                    .OrderBy(s => s.StrategicAxis.OrderIndex).ThenBy(s => s.OrderIndex)
                    .Select(s => new { s.Id, Name = s.StrategicAxis.NameAr + " > " + s.NameAr }).ToListAsync(),
                "Id", "Name", obj.StrategicObjectiveId);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMainObjective(MainObjectiveFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.MainObjectives.FindAsync(model.Id);
            if (obj == null) return NotFound();
            model.UpdateEntity(obj);
            obj.LastModifiedById = GetCurrentUserId();
            obj.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMainObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
            var obj = await _db.MainObjectives.FindAsync(id);
            if (obj == null) return NotFound();
            if (await _db.SubObjectives.AnyAsync(s => s.MainObjectiveId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الهدف الرئيسي لوجود أهداف فرعية مرتبطة به";
                return RedirectToAction(nameof(Index));
            }
            _db.MainObjectives.Remove(obj);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الهدف الرئيسي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Sub Objectives

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubObjective(string NameAr, string NameEn, string? DescriptionAr, int MainObjectiveId,
            Guid? ExternalUnitId, string? ExternalUnitName, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            var lastObj = await _db.SubObjectives.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
            string code = $"SUB-{((lastObj?.Id ?? 0) + 1):D2}";
            _db.SubObjectives.Add(new SubObjective
            {
                Code = code, NameAr = NameAr, NameEn = NameEn, DescriptionAr = DescriptionAr,
                MainObjectiveId = MainObjectiveId, ExternalUnitId = ExternalUnitId, ExternalUnitName = ExternalUnitName,
                OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditSubObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
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
            if (!IsAdmin()) return Forbid();
            var objective = await _db.SubObjectives.FindAsync(model.Id);
            if (objective == null) return NotFound();
            model.UpdateEntity(objective);
            objective.LastModifiedById = GetCurrentUserId();
            objective.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubObjective(int id)
        {
            if (!IsAdmin()) return Forbid();
            var objective = await _db.SubObjectives.FindAsync(id);
            if (objective == null) return NotFound();
            _db.SubObjectives.Remove(objective);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الهدف الفرعي بنجاح";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Core Values

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddValue(string NameAr, string NameEn, string? MeaningAr, string? Icon, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            _db.CoreValues.Add(new CoreValue
            {
                NameAr = NameAr, NameEn = NameEn, MeaningAr = MeaningAr, Icon = Icon,
                OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة القيمة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> EditValue(int id)
        {
            if (!IsAdmin()) return Forbid();
            var value = await _db.CoreValues.FindAsync(id);
            if (value == null) return NotFound();
            return View(CoreValueFormViewModel.FromEntity(value));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditValue(CoreValueFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            var value = await _db.CoreValues.FindAsync(model.Id);
            if (value == null) return NotFound();
            model.UpdateEntity(value);
            value.LastModifiedById = GetCurrentUserId();
            value.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث القيمة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteValue(int id)
        {
            if (!IsAdmin()) return Forbid();
            var value = await _db.CoreValues.FindAsync(id);
            if (value == null) return NotFound();
            _db.CoreValues.Remove(value);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف القيمة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Financial Costs

        public async Task<IActionResult> FinancialCosts()
        {
            if (!IsAdmin()) return Forbid();
            return View(await _db.FinancialCosts.OrderBy(c => c.OrderIndex).ToListAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFinancialCost(string NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, int OrderIndex, bool IsActive = true)
        {
            if (!IsAdmin()) return Forbid();
            _db.FinancialCosts.Add(new FinancialCost
            {
                NameAr = NameAr, NameEn = NameEn, DescriptionAr = DescriptionAr, DescriptionEn = DescriptionEn,
                OrderIndex = OrderIndex, IsActive = IsActive,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        public async Task<IActionResult> EditFinancialCost(int id)
        {
            if (!IsAdmin()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null) return NotFound();
            return View(FinancialCostFormViewModel.FromEntity(cost));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFinancialCost(FinancialCostFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
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
            if (!IsAdmin()) return Forbid();
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
            if (!IsAdmin()) return Forbid();
            var cost = await _db.FinancialCosts.FindAsync(id);
            if (cost == null) return NotFound();
            _db.FinancialCosts.Remove(cost);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف التكلفة المالية بنجاح";
            return RedirectToAction(nameof(FinancialCosts));
        }

        #endregion
    }
}
