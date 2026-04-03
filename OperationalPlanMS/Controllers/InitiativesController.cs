using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
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
        public async Task<IActionResult> Index(InitiativeListViewModel model, Guid? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            // StepUser and User can now see initiatives if they have access via InitiativeAccess
            if (userRole == UserRole.StepUser || userRole == UserRole.User)
            {
                var hasAnyAccess = _initiativeService.GetType()
                    .GetMethod("CanAccess") != null; // always true, just check DB
                // Check if user has any initiative access
                var db = HttpContext.RequestServices.GetRequiredService<OperationalPlanMS.Data.AppDbContext>();
                var hasInitiativeAccess = db.InitiativeAccess.Any(a => a.UserId == userId && a.IsActive);
                if (!hasInitiativeAccess)
                {
                    if (userRole == UserRole.StepUser)
                        return RedirectToAction("Index", "Home");
                    if (userRole == UserRole.User)
                        return RedirectToAction("Index", "Projects");
                }
            }

            var viewModel = await _initiativeService.GetListAsync(
                model.SearchTerm, model.FiscalYearId, externalUnitId,
                model.CurrentPage, model.PageSize, userRole, userId);

            ViewBag.CanEdit = CanEditInitiatives();
            ViewBag.UserRole = userRole;

            if (externalUnitId.HasValue)
                ViewBag.SelectedUnitName = await _initiativeService.GetUnitNameAsync(externalUnitId.Value);
            ViewBag.ExternalUnitId = externalUnitId;
            return View(viewModel);
        }

        // GET: /Initiatives/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var viewModel = await _initiativeService.GetDetailsAsync(id);
            if (viewModel == null) return NotFound();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (!_initiativeService.CanAccess(viewModel.Initiative, userRole, userId))
                return Forbid();

            var accessLevel = _initiativeService.GetAccessLevel(id, userRole, userId);

            // CanEdit: Admin always, Supervisor of this initiative, or Contributor/FullAccess
            ViewBag.CanEdit = (userRole == UserRole.Admin || userRole == UserRole.SuperAdmin)
                || (userRole == UserRole.Supervisor && viewModel.Initiative.SupervisorId == userId)
                || (accessLevel.HasValue && accessLevel.Value >= AccessLevel.Contributor);

            ViewBag.UserRole = userRole;
            ViewBag.AccessLevel = accessLevel;
            ViewBag.IsAdmin = userRole == UserRole.Admin || userRole == UserRole.SuperAdmin;
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
            if (GetCurrentUserRole() != UserRole.Admin && GetCurrentUserRole() != UserRole.SuperAdmin) return Forbid();

            var (success, error) = await _initiativeService.EditNoteAsync(noteId, initiativeId, notes);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم تعديل الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        // POST: /Initiatives/DeleteNote
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int initiativeId)
        {
            if (GetCurrentUserRole() != UserRole.Admin && GetCurrentUserRole() != UserRole.SuperAdmin) return Forbid();

            var (success, error) = await _initiativeService.DeleteNoteAsync(noteId, initiativeId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "تم حذف الملاحظة بنجاح" : error;
            return RedirectToAction(nameof(Details), new { id = initiativeId });
        }

        // ═══════════════════════════════════════════════════════════
        //  Initiative Access Management Page — Admin only
        // ═══════════════════════════════════════════════════════════

        // GET: /Initiatives/AccessManagement
        [HttpGet]
        public async Task<IActionResult> AccessManagement()
        {
            if (!IsAdmin()) return Forbid();

            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var initiatives = await db.Initiatives
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.NameAr)
                .ToListAsync();

            return View(initiatives);
        }

        // ═══════════════════════════════════════════════════════════
        //  Initiative Access (Members) API — Admin only
        // ═══════════════════════════════════════════════════════════

        // GET: /Initiatives/GetMembers/5
        [HttpGet]
        public async Task<IActionResult> GetMembers(int id)
        {
            if (!IsAdmin()) return Forbid();

            var db = HttpContext.RequestServices.GetRequiredService<OperationalPlanMS.Data.AppDbContext>();
            var members = await db.InitiativeAccess
                .Where(a => a.InitiativeId == id && a.IsActive)
                .Include(a => a.User)
                .Include(a => a.GrantedBy)
                .OrderBy(a => a.GrantedAt)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    UserName = a.User.FullNameAr,
                    UserUnit = a.User.ExternalUnitName ?? "-",
                    UserRank = a.User.EmployeeRank ?? "",
                    a.AccessLevel,
                    AccessLevelText = a.AccessLevel == AccessLevel.ReadOnly ? "اطلاع فقط"
                        : a.AccessLevel == AccessLevel.Contributor ? "مساهم"
                        : "وصول كامل",
                    GrantedByName = a.GrantedBy != null ? a.GrantedBy.FullNameAr : "-",
                    a.GrantedAt
                })
                .ToListAsync();

            return Json(members);
        }

        // POST: /Initiatives/AddMember
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddMember([FromBody] AddMemberDto dto)
        {
            if (!IsAdmin()) return Json(new { success = false, error = "غير مصرح" });

            var db = HttpContext.RequestServices.GetRequiredService<OperationalPlanMS.Data.AppDbContext>();

            // Check if already exists
            var existing = await db.InitiativeAccess
                .FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.InitiativeId == dto.InitiativeId);

            if (existing != null)
            {
                if (existing.IsActive)
                    return Json(new { success = false, error = "هذا المستخدم لديه صلاحية وصول بالفعل" });

                // Reactivate
                existing.IsActive = true;
                existing.AccessLevel = (AccessLevel)dto.AccessLevel;
                existing.GrantedById = GetCurrentUserId();
                existing.GrantedAt = DateTime.Now;
            }
            else
            {
                db.InitiativeAccess.Add(new OperationalPlanMS.Models.Entities.InitiativeAccess
                {
                    UserId = dto.UserId,
                    InitiativeId = dto.InitiativeId,
                    AccessLevel = (AccessLevel)dto.AccessLevel,
                    GrantedById = GetCurrentUserId(),
                    GrantedAt = DateTime.Now,
                    IsActive = true
                });
            }

            await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /Initiatives/UpdateMemberAccess
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateMemberAccess([FromBody] UpdateMemberDto dto)
        {
            if (!IsAdmin()) return Json(new { success = false, error = "غير مصرح" });

            var db = HttpContext.RequestServices.GetRequiredService<OperationalPlanMS.Data.AppDbContext>();
            var access = await db.InitiativeAccess.FindAsync(dto.Id);
            if (access == null) return Json(new { success = false, error = "السجل غير موجود" });

            access.AccessLevel = (AccessLevel)dto.AccessLevel;
            await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /Initiatives/RemoveMember
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemoveMember([FromBody] RemoveMemberDto dto)
        {
            if (!IsAdmin()) return Json(new { success = false, error = "غير مصرح" });

            var db = HttpContext.RequestServices.GetRequiredService<OperationalPlanMS.Data.AppDbContext>();
            var access = await db.InitiativeAccess.FindAsync(dto.Id);
            if (access == null) return Json(new { success = false, error = "السجل غير موجود" });

            access.IsActive = false;
            await db.SaveChangesAsync();
            return Json(new { success = true });
        }

        // GET: /Initiatives/SearchUser?empNumber=12345&initiativeId=1
        [HttpGet]
        public async Task<IActionResult> SearchUser(string empNumber, int initiativeId)
        {
            if (!IsAdmin()) return Json(new { found = false, error = "غير مصرح" });
            if (string.IsNullOrWhiteSpace(empNumber))
                return Json(new { found = false, error = "أدخل رقم الموظف" });

            var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var user = await db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ADUsername == empNumber.Trim() && u.IsActive);

            if (user == null)
                return Json(new { found = false, error = "لم يتم العثور على موظف بهذا الرقم" });

            // Check if already has access
            var hasAccess = await db.InitiativeAccess
                .AnyAsync(a => a.UserId == user.Id && a.InitiativeId == initiativeId && a.IsActive);

            if (hasAccess)
                return Json(new { found = false, error = "هذا الموظف لديه صلاحية وصول بالفعل لهذه المبادرة" });

            // Check if is supervisor
            var initiative = await db.Initiatives.FindAsync(initiativeId);
            if (initiative?.SupervisorId == user.Id)
                return Json(new { found = false, error = "هذا الموظف هو المشرف على هذه المبادرة" });

            return Json(new
            {
                found = true,
                userId = user.Id,
                fullNameAr = user.FullNameAr,
                fullNameEn = user.FullNameEn,
                empNumber = user.ADUsername,
                rank = user.EmployeeRank ?? "",
                position = user.EmployeePosition ?? "",
                unit = user.ExternalUnitName ?? "-",
                role = user.Role?.NameAr ?? "-",
                email = user.Email ?? "-"
            });
        }
    }

    // ═══ DTOs for Member Management ═══
    public class AddMemberDto
    {
        public int UserId { get; set; }
        public int InitiativeId { get; set; }
        public int AccessLevel { get; set; }
    }

    public class UpdateMemberDto
    {
        public int Id { get; set; }
        public int AccessLevel { get; set; }
    }

    public class RemoveMemberDto
    {
        public int Id { get; set; }
    }
}
