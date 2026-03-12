using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers
{
    /// <summary>
    /// إدارة المستخدمين — Controller مستقل
    /// </summary>
    [Authorize]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: /Users
        public async Task<IActionResult> Index(string? searchTerm, int? roleId, bool? isActive, int page = 1)
        {
            if (!IsAdmin()) return Forbid();

            var viewModel = await _userService.GetUsersAsync(searchTerm, roleId, isActive, page);
            return View(viewModel);
        }

        // GET: /Users/Create
        public async Task<IActionResult> Create()
        {
            if (!IsAdmin()) return Forbid();

            var viewModel = await _userService.PrepareCreateViewModelAsync();
            return View(viewModel);
        }

        // POST: /Users/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();

            if (!ModelState.IsValid)
            {
                await _userService.PopulateDropdownsAsync(model);
                return View(model);
            }

            var (success, error) = await _userService.CreateAsync(model, GetCurrentUserId());

            if (!success)
            {
                ModelState.AddModelError("ADUsername", error!);
                await _userService.PopulateDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "تم إضافة المستخدم بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin()) return Forbid();

            try
            {
                var viewModel = await _userService.GetFormViewModelAsync(id);
                return View(viewModel);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // POST: /Users/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await _userService.PopulateDropdownsAsync(model);
                return View(model);
            }

            var (success, error) = await _userService.UpdateAsync(id, model);

            if (!success)
            {
                ModelState.AddModelError("ADUsername", error!);
                await _userService.PopulateDropdownsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "تم تحديث المستخدم بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin()) return Forbid();

            var (success, error) = await _userService.DeleteAsync(id, GetCurrentUserId());

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف المستخدم بنجاح" : error;
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/ToggleActive/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!IsAdmin()) return Forbid();

            var (success, message) = await _userService.ToggleActiveAsync(id);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
