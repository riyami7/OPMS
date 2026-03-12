using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class SupportingEntitiesController : BaseController
    {
        private readonly AppDbContext _db;

        public SupportingEntitiesController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(string? searchTerm, bool? isActive)
        {
            if (!IsAdmin()) return Forbid();

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

        public IActionResult Create()
        {
            if (!IsAdmin()) return Forbid();
            return View(new SupportingEntityFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupportingEntityFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();

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
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();
            return View(SupportingEntityFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupportingEntityFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
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
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();

            if (await _db.ProjectSupportingUnits.AnyAsync(p => p.SupportingEntityId == id))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف جهة المساندة لوجود مشاريع مرتبطة بها";
                return RedirectToAction(nameof(Index));
            }

            _db.SupportingEntities.Remove(entity);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف جهة المساندة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.SupportingEntities.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = !entity.IsActive;
            entity.LastModifiedById = GetCurrentUserId();
            entity.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = entity.IsActive ? "تم تفعيل جهة المساندة" : "تم تعطيل جهة المساندة";
            return RedirectToAction(nameof(Index));
        }
    }
}
