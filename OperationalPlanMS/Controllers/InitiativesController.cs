using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class InitiativesController : BaseController
    {
        private readonly IInitiativeService _initiativeService;

        public InitiativesController(IInitiativeService initiativeService)
        {
            _initiativeService = initiativeService;
        }

        // GET: /Initiatives
        public async Task<IActionResult> Index(InitiativeListViewModel model, int? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.StepUser)
                return RedirectToAction("Index", "Home");
            if (userRole == UserRole.User)
                return RedirectToAction("Index", "Projects");

            var viewModel = await _initiativeService.GetListAsync(
                model.SearchTerm, model.FiscalYearId, externalUnitId,
                model.CurrentPage, model.PageSize, userRole, userId);

            ViewBag.CanEdit = CanEditInitiatives();
            ViewBag.UserRole = userRole;

            if (externalUnitId.HasValue)
                ViewBag.SelectedUnitName = await _initiativeService.GetUnitNameAsync(externalUnitId.Value);

            return View(viewModel);
        }

        // GET: /Initiatives/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _initiativeService.GetDetailsAsync(id);
            if (viewModel == null) return NotFound();
            if (!_initiativeService.CanAccess(viewModel.Initiative, GetCurrentUserRole(), GetCurrentUserId()))
                return Forbid();

            ViewBag.CanEdit = CanEditInitiatives() &&
                _initiativeService.CanAccess(viewModel.Initiative, GetCurrentUserRole(), GetCurrentUserId());
            ViewBag.UserRole = GetCurrentUserRole();
            return View(viewModel);
        }

        // GET: /Initiatives/Create
        public async Task<IActionResult> Create()
        {
            if (!CanEditInitiatives()) return Forbid();
            var viewModel = await _initiativeService.PrepareCreateViewModelAsync();
            return View(viewModel);
        }

        // POST: /Initiatives/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InitiativeFormViewModel model)
        {
            if (!CanEditInitiatives()) return Forbid();

            if (!ModelState.IsValid)
            {
                await _initiativeService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            var (success, id, error) = await _initiativeService.CreateAsync(model, GetCurrentUserId());

            if (!success)
            {
                ModelState.AddModelError("Code", error!);
                await _initiativeService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "تم إضافة المبادرة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Initiatives/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEditInitiatives()) return Forbid();

            var viewModel = await _initiativeService.PrepareEditViewModelAsync(id);
            if (viewModel == null) return NotFound();

            // Supervisor يعدل مبادراته فقط
            var initiative = await _initiativeService.GetByIdAsync(id);
            if (IsSupervisor() && initiative?.SupervisorId != GetCurrentUserId()) return Forbid();

            return View(viewModel);
        }

        // POST: /Initiatives/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InitiativeFormViewModel model)
        {
            if (!CanEditInitiatives()) return Forbid();
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await _initiativeService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            var (success, error) = await _initiativeService.UpdateAsync(
                id, model, GetCurrentUserId(), GetCurrentUserRole(), GetCurrentUserId());

            if (!success)
            {
                ModelState.AddModelError("Code", error!);
                await _initiativeService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "تم تحديث المبادرة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Initiatives/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanEditInitiatives()) return Forbid();

            var initiative = await _initiativeService.GetByIdAsync(id);
            if (initiative == null) return NotFound();
            if (IsSupervisor() && initiative.SupervisorId != GetCurrentUserId()) return Forbid();

            return View(initiative);
        }

        // POST: /Initiatives/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanEditInitiatives()) return Forbid();

            var (success, error) = await _initiativeService.SoftDeleteAsync(
                id, GetCurrentUserId(), GetCurrentUserRole(), GetCurrentUserId());

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف المبادرة بنجاح" : error;
            return RedirectToAction(nameof(Index));
        }

        // POST: /Initiatives/AddNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string notes)
        {
            var (success, error) = await _initiativeService.AddNoteAsync(id, notes, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم إضافة الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Initiatives/EditNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int initiativeId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();

            var (success, error) = await _initiativeService.EditNoteAsync(noteId, initiativeId, notes);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تعديل الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        // POST: /Initiatives/DeleteNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int initiativeId)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();

            var (success, error) = await _initiativeService.DeleteNoteAsync(noteId, initiativeId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }
    }
}
