using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class RolesController : BaseController
    {
        private readonly AppDbContext _db;

        public RolesController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();
            var viewModel = new RoleListViewModel
            {
                Roles = await _db.Roles.ToListAsync(),
                TotalCount = await _db.Roles.CountAsync()
            };
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.Roles.FindAsync(id);
            if (entity == null) return NotFound();
            return View(RoleFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RoleFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var entity = await _db.Roles.FindAsync(id);
                if (entity == null) return NotFound();
                model.UpdateEntity(entity);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث الدور بنجاح";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
    }
}
