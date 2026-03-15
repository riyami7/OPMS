using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class ProjectsController : BaseController
    {
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        // GET: /Projects
        public async Task<IActionResult> Index(ProjectListViewModel model, int? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var viewModel = await _projectService.GetListAsync(
                model.SearchTerm, model.InitiativeId, externalUnitId,
                model.CurrentPage, model.PageSize, userRole, userId);

            ViewBag.CanEdit = CanEditProjects();
            ViewBag.UserRole = userRole;
            ViewBag.ExternalUnitId = externalUnitId;

            if (externalUnitId.HasValue)
                ViewBag.SelectedUnitName = await _projectService.GetUnitNameAsync(externalUnitId.Value);

            return View(viewModel);
        }

        // API: GET /Projects/GetSupportingEntities
        [HttpGet]
        public async Task<IActionResult> GetSupportingEntities() =>
            Json(await _projectService.GetSupportingEntitiesAsync());

        // API: GET /Projects/GetSupportingEntityInfo?id=5
        [HttpGet]
        public async Task<IActionResult> GetSupportingEntityInfo(int id)
        {
            var result = await _projectService.GetSupportingEntityInfoAsync(id);
            return result == null ? NotFound() : Json(result);
        }

        // API: GET /Projects/GetSubObjectivesByUnit?externalUnitId=5
        [HttpGet]
        public async Task<IActionResult> GetSubObjectivesByUnit(int? externalUnitId) =>
            Json(await _projectService.GetSubObjectivesByUnitAsync(externalUnitId));

        // GET: /Projects/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _projectService.GetDetailsAsync(id);
            if (viewModel == null) return NotFound();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (!_projectService.CanAccess(viewModel.Project, userRole, userId))
                return Forbid();

            ViewBag.CanEdit = CanEditProjects() || CanEditInitiativeContent(viewModel.Project.InitiativeId);
            ViewBag.UserRole = userRole;
            ViewBag.CurrentUserId = userId;
            return View(viewModel);
        }

        // GET: /Projects/Create?initiativeId=5
        public async Task<IActionResult> Create(int? initiativeId)
        {
            if (!initiativeId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب تحديد المبادرة لإضافة مشروع";
                return RedirectToAction("Index", "Initiatives");
            }
            if (!CanEditProjects() && !CanEditInitiativeContent(initiativeId.Value)) return Forbid();

            var viewModel = await _projectService.PrepareCreateViewModelAsync(initiativeId.Value);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        // POST: /Projects/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            if (!CanEditProjects() && !CanEditInitiativeContent(model.InitiativeId)) return Forbid();

            if (!ModelState.IsValid)
            {
                await _projectService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            var (success, initiativeId, error, warning) = await _projectService.CreateAsync(model, GetCurrentUserId());

            if (!success)
            {
                ModelState.AddModelError(error!.Contains("كود") ? "Code" : "ProjectNumber", error!);
                await _projectService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            if (warning != null) TempData["WarningMessage"] = warning;
            TempData["SuccessMessage"] = "تم إضافة المشروع بنجاح";
            return RedirectToAction("Details", "Initiatives", new { id = initiativeId });
        }

        // GET: /Projects/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var viewModel = await _projectService.PrepareEditViewModelAsync(id);
            if (viewModel == null) return NotFound();
            if (!CanEditProjects() && !CanEditInitiativeContent(viewModel.InitiativeId)) return Forbid();

            ViewBag.CalculatedProgress = await _projectService.GetCalculatedProgressAsync(id);
            return View(viewModel);
        }

        // POST: /Projects/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
        {
            if (!CanEditProjects() && !CanEditInitiativeContent(model.InitiativeId)) return Forbid();
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await _projectService.PopulateFormDropdownsAsync(model);
                ViewBag.CalculatedProgress = await _projectService.GetCalculatedProgressAsync(id);
                return View(model);
            }

            var (success, error, warning) = await _projectService.UpdateAsync(id, model, GetCurrentUserId());

            if (!success)
            {
                ModelState.AddModelError(error!.Contains("كود") ? "Code" : "ProjectNumber", error!);
                await _projectService.PopulateFormDropdownsAsync(model);
                ViewBag.CalculatedProgress = await _projectService.GetCalculatedProgressAsync(id);
                return View(model);
            }

            if (warning != null) TempData["WarningMessage"] = warning;
            TempData["SuccessMessage"] = "تم تحديث المشروع بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Projects/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _projectService.GetWithInitiativeAsync(id);
            if (project == null) return NotFound();
            if (!CanEditProjects() && !CanEditInitiativeContent(project.InitiativeId)) return Forbid();
            if (IsSupervisor() && project.Initiative?.SupervisorId != GetCurrentUserId()) return Forbid();
            return View(project);
        }

        // POST: /Projects/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _projectService.GetWithInitiativeAsync(id);
            if (project == null) return NotFound();
            if (!CanEditProjects() && !CanEditInitiativeContent(project.InitiativeId)) return Forbid();
            if (IsSupervisor() && project.Initiative?.SupervisorId != GetCurrentUserId()) return Forbid();

            var (success, error) = await _projectService.SoftDeleteAsync(id, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف المشروع بنجاح" : error;
            return RedirectToAction("Details", "Initiatives", new { id = project.InitiativeId });
        }

        // POST: /Projects/AddNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string note)
        {
            var (success, error) = await _projectService.AddNoteAsync(id, note, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم إضافة الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Projects/EditNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int projectId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _projectService.EditNoteAsync(noteId, projectId, notes);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تعديل الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: /Projects/DeleteNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int projectId)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _projectService.DeleteNoteAsync(noteId, projectId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: /Projects/RecalculateProgress
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateProgress(int id)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project == null) return NotFound();
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            if (!(userRole == UserRole.Admin || (userRole == UserRole.User && project.ProjectManagerId == userId)))
                return Forbid();

            await _projectService.RecalculateProgressAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
