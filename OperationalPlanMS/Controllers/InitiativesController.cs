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
    public class InitiativesController : BaseController
    {
        private readonly AppDbContext _db;

        public InitiativesController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index(InitiativeListViewModel model, int? externalUnitId)
        {
            var query = _db.Initiatives.Where(i => !i.IsDeleted)
                .Include(i => i.FiscalYear).Include(i => i.Supervisor)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            // StepUser لا يرى المبادرات
            if (userRole == UserRole.StepUser)
                return RedirectToAction("Index", "Home");

            if (userRole == UserRole.Supervisor)
                query = query.Where(i => i.SupervisorId == userId);
            else if (userRole == UserRole.User)
                return RedirectToAction("Index", "Projects");

            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
                query = query.Where(i => i.NameAr.Contains(model.SearchTerm) ||
                    i.NameEn.Contains(model.SearchTerm) || i.Code.Contains(model.SearchTerm));

            if (model.FiscalYearId.HasValue)
                query = query.Where(i => i.FiscalYearId == model.FiscalYearId.Value);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                query = query.Where(i => i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            model.TotalCount = await query.CountAsync();
            model.Initiatives = await query.OrderByDescending(i => i.CreatedAt)
                .Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).ToListAsync();

            await PopulateFilterDropdowns(model);
            ViewBag.CanEdit = CanEditInitiatives();
            ViewBag.UserRole = userRole;

            if (externalUnitId.HasValue)
            {
                var selectedUnit = await _db.ExternalOrganizationalUnits.FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                ViewBag.SelectedUnitName = selectedUnit?.ArabicName ?? selectedUnit?.ArabicUnitName;
            }

            return View(model);
        }

        private async Task<List<int>> GetUnitAndChildrenIds(int unitId)
        {
            var result = new List<int> { unitId };
            var children = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == unitId && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var childId in children)
            {
                result.Add(childId);
                var grandChildren = await _db.ExternalOrganizationalUnits
                    .Where(u => u.ParentId == childId && u.IsActive).Select(u => u.Id).ToListAsync();
                result.AddRange(grandChildren);
            }
            return result;
        }

        public async Task<IActionResult> Details(int id)
        {
            var initiative = await _db.Initiatives.Include(i => i.FiscalYear)
                .Include(i => i.Supervisor).Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return NotFound();
            if (!CanAccessInitiative(initiative)) return Forbid();

            var viewModel = new InitiativeDetailsViewModel
            {
                Initiative = initiative,
                Projects = await _db.Projects.Where(p => p.InitiativeId == id && !p.IsDeleted)
                    .Include(p => p.ProjectManager).Include(p => p.Steps.Where(s => !s.IsDeleted)).ToListAsync(),
                Notes = await _db.ProgressUpdates.Where(p => p.InitiativeId == id)
                    .Include(p => p.CreatedBy).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync()
            };

            ViewBag.CanEdit = CanEditInitiatives() && CanAccessInitiative(initiative);
            ViewBag.UserRole = GetCurrentUserRole();
            return View(viewModel);
        }

        public async Task<IActionResult> Create()
        {
            if (!CanEditInitiatives()) return Forbid();
            var viewModel = new InitiativeFormViewModel();
            var currentYear = DateTime.Now.Year;
            var lastCode = await _db.Initiatives.Where(i => i.Code.StartsWith($"INI-{currentYear}"))
                .OrderByDescending(i => i.Code).Select(i => i.Code).FirstOrDefaultAsync();
            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var parts = lastCode.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int last)) nextNumber = last + 1;
            }
            viewModel.Code = $"INI-{currentYear}-{nextNumber:D3}";
            await PopulateFormDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InitiativeFormViewModel model)
        {
            if (!CanEditInitiatives()) return Forbid();
            if (ModelState.IsValid)
            {
                if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model); return View(model);
                }
                var initiative = new Initiative { CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now };
                model.UpdateEntity(initiative);
                _db.Initiatives.Add(initiative);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة المبادرة بنجاح";
                return RedirectToAction(nameof(Details), new { id = initiative.Id });
            }
            await PopulateFormDropdowns(model);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEditInitiatives()) return Forbid();
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return NotFound();
            // Supervisor يعدل مبادراته فقط
            if (IsSupervisor() && initiative.SupervisorId != GetCurrentUserId()) return Forbid();
            var viewModel = InitiativeFormViewModel.FromEntity(initiative);
            await PopulateFormDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InitiativeFormViewModel model)
        {
            if (!CanEditInitiatives()) return Forbid();
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
                if (initiative == null) return NotFound();
                // Supervisor يعدل مبادراته فقط
                if (IsSupervisor() && initiative.SupervisorId != GetCurrentUserId()) return Forbid();
                if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code && i.Id != id))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model); return View(model);
                }
                model.UpdateEntity(initiative);
                initiative.LastModifiedById = GetCurrentUserId();
                initiative.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث المبادرة بنجاح";
                return RedirectToAction(nameof(Details), new { id = initiative.Id });
            }
            await PopulateFormDropdowns(model);
            return View(model);
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanEditInitiatives()) return Forbid();
            var initiative = await _db.Initiatives.Include(i => i.FiscalYear)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return NotFound();
            // Supervisor يحذف مبادراته فقط
            if (IsSupervisor() && initiative.SupervisorId != GetCurrentUserId()) return Forbid();
            return View(initiative);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanEditInitiatives()) return Forbid();
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return NotFound();
            // Supervisor يحذف مبادراته فقط
            if (IsSupervisor() && initiative.SupervisorId != GetCurrentUserId()) return Forbid();
            initiative.IsDeleted = true;
            initiative.LastModifiedById = GetCurrentUserId();
            initiative.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المبادرة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string notes)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return NotFound();
            if (string.IsNullOrWhiteSpace(notes)) { TempData["ErrorMessage"] = "الملاحظة مطلوبة"; return RedirectToAction(nameof(Details), new { id }); }
            _db.ProgressUpdates.Add(new ProgressUpdate { InitiativeId = id, NotesAr = notes, UpdateType = UpdateType.Note, CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now });
            initiative.LastModifiedById = GetCurrentUserId(); initiative.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int initiativeId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId) return NotFound();
            if (string.IsNullOrWhiteSpace(notes)) { TempData["ErrorMessage"] = "الملاحظة مطلوبة"; return RedirectToAction(nameof(Details), new { id = initiativeId }); }
            note.NotesAr = notes;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تعديل الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int initiativeId)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId) return NotFound();
            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        #region Helpers

        private bool CanAccessInitiative(Initiative initiative)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            return userRole switch
            {
                UserRole.Admin => true,
                UserRole.Executive => true,
                UserRole.Supervisor => initiative.SupervisorId == userId,
                _ => false
            };
        }

        private async Task PopulateFilterDropdowns(InitiativeListViewModel model)
        {
            model.FiscalYears = new SelectList(await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(), "Id", "NameAr", model.FiscalYearId);
        }

        private async Task PopulateFormDropdowns(InitiativeFormViewModel model)
        {
            model.FiscalYears = new SelectList(await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(), "Id", "NameAr", model.FiscalYearId);
            model.Supervisors = new SelectList(await _db.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullNameAr", model.SupervisorId);
        }

        #endregion
    }
}