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
    public class FiscalYearsController : BaseController
    {
        private readonly AppDbContext _db;

        public FiscalYearsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return Forbid();

            var viewModel = new FiscalYearListViewModel
            {
                FiscalYears = await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                TotalCount = await _db.FiscalYears.CountAsync()
            };

            return View(viewModel);
        }

        public IActionResult Create()
        {
            if (!IsAdmin()) return Forbid();

            return View(new FiscalYearFormViewModel
            {
                Year = DateTime.Now.Year,
                NameAr = $"السنة المالية {DateTime.Now.Year}",
                NameEn = $"Fiscal Year {DateTime.Now.Year}",
                StartDate = new DateTime(DateTime.Now.Year, 1, 1),
                EndDate = new DateTime(DateTime.Now.Year, 12, 31)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FiscalYearFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();

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
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();
            return View(FiscalYearFormViewModel.FromEntity(entity));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FiscalYearFormViewModel model)
        {
            if (!IsAdmin()) return Forbid();
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
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();

            

            _db.FiscalYears.Remove(entity);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف السنة المالية بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            if (!IsAdmin()) return Forbid();
            var entity = await _db.FiscalYears.FindAsync(id);
            if (entity == null) return NotFound();

            await _db.FiscalYears.Where(f => f.IsCurrent)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false));

            entity.IsCurrent = true;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تعيين السنة المالية الحالية";
            return RedirectToAction(nameof(Index));
        }
    }
}

