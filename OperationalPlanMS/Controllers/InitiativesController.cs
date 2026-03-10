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

        public InitiativesController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Initiatives
        // تم تعديل الفلتر ليدعم ExternalUnitId
        public async Task<IActionResult> Index(InitiativeListViewModel model, int? externalUnitId)
        {
            var query = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.FiscalYear)
                .Include(i => i.Supervisor)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .AsQueryable();

            // Apply role-based filtering
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor)
            {
                query = query.Where(i => i.SupervisorId == userId);
            }
            else if (userRole == UserRole.User)
            {
                return RedirectToAction("Index", "Projects");
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                query = query.Where(i =>
                    i.NameAr.Contains(model.SearchTerm) ||
                    i.NameEn.Contains(model.SearchTerm) ||
                    i.Code.Contains(model.SearchTerm));
            }

            if (model.FiscalYearId.HasValue)
            {
                query = query.Where(i => i.FiscalYearId == model.FiscalYearId.Value);
            }

            // فلتر الوحدة التنظيمية من API الخارجي
            if (externalUnitId.HasValue)
            {
                // جلب الوحدة المختارة وجميع الوحدات الفرعية
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                query = query.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            if (model.OrganizationId.HasValue)
            {
            }

            model.TotalCount = await query.CountAsync();

            model.Initiatives = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            await PopulateFilterDropdowns(model);

            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = userRole;

            // تمرير ExternalUnitId للـ View
            ViewBag.ExternalUnitId = externalUnitId;

            // جلب اسم الوحدة المختارة
            if (externalUnitId.HasValue)
            {
                var selectedUnit = await _db.ExternalOrganizationalUnits
                    .FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                ViewBag.SelectedUnitName = selectedUnit?.ArabicName ?? selectedUnit?.ArabicUnitName;
            }

            return View(model);
        }

        /// <summary>
        /// جلب الوحدة وجميع الوحدات الفرعية (للفلترة الهرمية)
        /// </summary>
        private async Task<List<int>> GetUnitAndChildrenIds(int unitId)
        {
            var result = new List<int> { unitId };

            // جلب الأبناء المباشرين (المستوى الثاني)
            var children = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == unitId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var childId in children)
            {
                result.Add(childId);

                // جلب الأحفاد (المستوى الثالث)
                var grandChildren = await _db.ExternalOrganizationalUnits
                    .Where(u => u.ParentId == childId && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                result.AddRange(grandChildren);
            }

            return result;
        }

        // GET: /Initiatives/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var initiative = await _db.Initiatives
                .Include(i => i.FiscalYear)
                .Include(i => i.Supervisor)
                .Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }

            if (!CanAccessInitiative(initiative))
            {
                return Forbid();
            }

            var viewModel = new InitiativeDetailsViewModel
            {
                Initiative = initiative,
                Projects = await _db.Projects
                    .Where(p => p.InitiativeId == id && !p.IsDeleted)
                    .Include(p => p.ProjectManager)
                    .Include(p => p.Steps.Where(s => !s.IsDeleted))
                    .ToListAsync(),
                Notes = await _db.ProgressUpdates
                    .Where(p => p.InitiativeId == id)
                    .Include(p => p.CreatedBy)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(20)
                    .ToListAsync()
            };

            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = GetCurrentUserRole();

            return View(viewModel);
        }

        // GET: /Initiatives/Create
        public async Task<IActionResult> Create()
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var viewModel = new InitiativeFormViewModel();

            // Generate next code
            var currentYear = DateTime.Now.Year;
            var lastCode = await _db.Initiatives
                .Where(i => i.Code.StartsWith($"INI-{currentYear}"))
                .OrderByDescending(i => i.Code)
                .Select(i => i.Code)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var parts = lastCode.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }
            viewModel.Code = $"INI-{currentYear}-{nextNumber:D3}";

            await PopulateFormDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Initiatives/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InitiativeFormViewModel model)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    return View(model);
                }

                var initiative = new Initiative
                {
                    CreatedById = GetCurrentUserId(),
                    CreatedAt = DateTime.Now
                };
                model.UpdateEntity(initiative);

                _db.Initiatives.Add(initiative);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم إضافة المبادرة بنجاح";
                return RedirectToAction(nameof(Details), new { id = initiative.Id });
            }

            await PopulateFormDropdowns(model);
            return View(model);
        }

        // GET: /Initiatives/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }

            var viewModel = InitiativeFormViewModel.FromEntity(initiative);
            await PopulateFormDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: /Initiatives/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InitiativeFormViewModel model)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var initiative = await _db.Initiatives
                    .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

                if (initiative == null)
                {
                    return NotFound();
                }

                if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code && i.Id != id))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    return View(model);
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

        // GET: /Initiatives/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var initiative = await _db.Initiatives
                .Include(i => i.FiscalYear)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }

            return View(initiative);
        }

        // POST: /Initiatives/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }

            initiative.IsDeleted = true;
            initiative.LastModifiedById = GetCurrentUserId();
            initiative.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف المبادرة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Initiatives/AddNote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string notes)
        {
            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id });
            }

            var progressUpdate = new ProgressUpdate
            {
                InitiativeId = id,
                NotesAr = notes,
                UpdateType = UpdateType.Note,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            initiative.LastModifiedById = GetCurrentUserId();
            initiative.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Initiatives/EditNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int initiativeId, string notes)
        {
            // فقط Admin يمكنه التعديل
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id = initiativeId });
            }

            note.NotesAr = notes;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعديل الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        // POST: /Initiatives/DeleteNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int initiativeId)
        {
            // فقط Admin يمكنه الحذف
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId)
            {
                return NotFound();
            }

            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        #region Helper Methods

        private bool CanAccessInitiative(Initiative initiative)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            return userRole switch
            {
                UserRole.Admin => true,
                UserRole.Executive => true,
                UserRole.Supervisor => initiative.SupervisorId == userId,
                UserRole.User => false,
                _ => false
            };
        }

        private async Task PopulateFilterDropdowns(InitiativeListViewModel model)
        {
                "Id", "NameAr", model.OrganizationId);

            model.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                "Id", "NameAr", model.FiscalYearId);

        }

        private async Task PopulateFormDropdowns(InitiativeFormViewModel model)
        {
                "Id", "NameAr", model.OrganizationId);

            model.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                "Id", "NameAr", model.FiscalYearId);

            if (model.OrganizationId > 0)
            {
                        .Where(u => u.IsActive && u.OrganizationId == model.OrganizationId)
                        .ToListAsync(),
            }
            else
            {
            }

            model.Supervisors = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(),
                "Id", "FullNameAr", model.SupervisorId);
        }


        #endregion
    }
}