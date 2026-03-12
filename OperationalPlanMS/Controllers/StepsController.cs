using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class StepsController : BaseController
    {
        private readonly IStepService _stepService;
        private readonly IWebHostEnvironment _env;

        public StepsController(IStepService stepService, IWebHostEnvironment env)
        {
            _stepService = stepService;
            _env = env;
        }

        public async Task<IActionResult> Index(StepListViewModel model)
        {
            var userRole = GetCurrentUserRole();
            var viewModel = await _stepService.GetListAsync(model, userRole, GetCurrentUserId());
            ViewBag.CanEdit = userRole == UserRole.Admin || userRole == UserRole.User;
            ViewBag.UserRole = userRole;
            return View(viewModel);
        }

        public async Task<IActionResult> PendingApprovals()
        {
            if (!await _stepService.IsStepApproverAsync(GetCurrentUserId()))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية الوصول لهذه الصفحة";
                return RedirectToAction("Index", "Home");
            }
            var pendingSteps = await _stepService.GetPendingApprovalsAsync();
            ViewBag.PendingCount = pendingSteps.Count;
            return View(pendingSteps);
        }

        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _stepService.GetDetailsAsync(id);
            if (viewModel == null) return NotFound();
            if (!_stepService.CanAccessStep(viewModel.Step, GetCurrentUserRole(), GetCurrentUserId()))
                return Forbid();

            ViewBag.CanEdit = _stepService.CanEditProject(viewModel.Step.Project, GetCurrentUserRole(), GetCurrentUserId());
            ViewBag.UserRole = GetCurrentUserRole();
            ViewBag.IsStepApprover = await _stepService.IsStepApproverAsync(GetCurrentUserId());
            return View(viewModel);
        }

        public async Task<IActionResult> Create(int? projectId)
        {
            if (!projectId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب تحديد المشروع لإضافة خطوة";
                return RedirectToAction("Index", "Projects");
            }

            var (viewModel, usedWeight, remainingWeight, projectName, initiativeName) =
                await _stepService.PrepareCreateViewModelAsync(projectId.Value);
            if (viewModel == null) return NotFound();

            ViewBag.ProjectName = projectName;
            ViewBag.InitiativeName = initiativeName;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StepFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _stepService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            var (success, projectId, error, warning) = await _stepService.CreateAsync(model, GetCurrentUserId());
            if (!success)
            {
                ModelState.AddModelError("Weight", error!);
                await _stepService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            if (warning != null) TempData["WarningMessage"] = warning;
            TempData["SuccessMessage"] = "تم إضافة الخطوة بنجاح";
            return RedirectToAction("Details", "Projects", new { id = projectId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var (viewModel, usedWeight, remainingWeight, projectName, initiativeName) =
                await _stepService.PrepareEditViewModelAsync(id);
            if (viewModel == null) return NotFound();

            ViewBag.ProjectName = projectName;
            ViewBag.InitiativeName = initiativeName;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StepFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await _stepService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            var (success, error, warning) = await _stepService.UpdateAsync(id, model, GetCurrentUserId());
            if (!success)
            {
                ModelState.AddModelError("Weight", error!);
                await _stepService.PopulateFormDropdownsAsync(model);
                return View(model);
            }

            if (warning != null) TempData["WarningMessage"] = warning;
            TempData["SuccessMessage"] = "تم تحديث الخطوة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var step = await _stepService.GetWithProjectAsync(id);
            if (step == null) return NotFound();
            if (!_stepService.CanEditProject(step.Project, GetCurrentUserRole(), GetCurrentUserId()))
                return Forbid();
            return View(step);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var step = await _stepService.GetWithProjectAsync(id);
            if (step == null) return NotFound();
            if (!_stepService.CanEditProject(step.Project, GetCurrentUserRole(), GetCurrentUserId()))
                return Forbid();

            var (success, projectId, error) = await _stepService.SoftDeleteAsync(id, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف الخطوة بنجاح" : error;
            return success ? RedirectToAction("Details", "Projects", new { id = projectId }) : RedirectToAction(nameof(Delete), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int id, decimal progress, string? notes)
        {
            var step = await _stepService.GetWithProjectAsync(id);
            if (step == null) return NotFound();
            if (!_stepService.CanAccessStep(step, GetCurrentUserRole(), GetCurrentUserId())) return Forbid();

            var (success, error) = await _stepService.UpdateProgressAsync(id, progress, notes, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تحديث نسبة الإنجاز بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id, string completionDetails, IFormFile attachmentFile)
        {
            var step = await _stepService.GetWithProjectAsync(id);
            if (step == null) return NotFound();
            if (!_stepService.CanAccessStep(step, GetCurrentUserRole(), GetCurrentUserId())) return Forbid();

            var (success, error) = await _stepService.SubmitForApprovalAsync(
                id, completionDetails, attachmentFile, GetCurrentUserId(), _env.WebRootPath);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم إرسال الخطوة للتأكيد بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStep(int id, string? approverNotes)
        {
            if (!await _stepService.IsStepApproverAsync(GetCurrentUserId())) return Forbid();
            var (success, error) = await _stepService.ApproveStepAsync(id, approverNotes, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تأكيد الخطوة بنجاح وتم احتساب الوزن في المشروع" : error;
            return RedirectToAction(nameof(PendingApprovals));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStep(int id, string rejectionReason)
        {
            if (!await _stepService.IsStepApproverAsync(GetCurrentUserId())) return Forbid();
            var (success, error) = await _stepService.RejectStepAsync(id, rejectionReason, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم رفض الخطوة وإرسالها للمسؤول للتعديل" : error;
            return success ? RedirectToAction(nameof(PendingApprovals)) : RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string notes)
        {
            var step = await _stepService.GetWithProjectAsync(id);
            if (step == null) return NotFound();
            if (!_stepService.CanAccessStep(step, GetCurrentUserRole(), GetCurrentUserId())) return Forbid();

            var (success, error) = await _stepService.AddNoteAsync(id, notes, GetCurrentUserId());
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم إضافة الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int stepId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _stepService.EditNoteAsync(noteId, stepId, notes);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تعديل الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = stepId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int stepId)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _stepService.DeleteNoteAsync(noteId, stepId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = stepId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkComplete(int id)
        {
            TempData["InfoMessage"] = "لإكمال الخطوة، يرجى استخدام نموذج 'إرسال للتأكيد' مع إرفاق ملف التوثيق";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
