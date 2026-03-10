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
    public class ProjectsController : BaseController
    {
        private readonly AppDbContext _db;

        public ProjectsController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index(ProjectListViewModel model, int? externalUnitId)
        {
            var query = _db.Projects.Where(p => !p.IsDeleted)
                .Include(p => p.Initiative).Include(p => p.ProjectManager)
                .Include(p => p.Steps.Where(s => !s.IsDeleted)).AsQueryable();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor)
            {
                var ids = await _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id).ToListAsync();
                query = query.Where(p => ids.Contains(p.InitiativeId));
            }
            else if (userRole == UserRole.User)
                query = query.Where(p => p.ProjectManagerId == userId);

            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
                query = query.Where(p => p.NameAr.Contains(model.SearchTerm) ||
                    p.NameEn.Contains(model.SearchTerm) || p.Code.Contains(model.SearchTerm));

            if (model.InitiativeId.HasValue)
                query = query.Where(p => p.InitiativeId == model.InitiativeId.Value);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                query = query.Where(p => p.Initiative.ExternalUnitId.HasValue &&
                    unitIds.Contains(p.Initiative.ExternalUnitId.Value));
            }

            model.TotalCount = await query.CountAsync();
            model.Projects = await query.OrderByDescending(p => p.CreatedAt)
                .Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).ToListAsync();

            foreach (var project in model.Projects)
                project.ProgressPercentage = CalculateProjectProgress(project);

            await PopulateFilterDropdowns(model);
            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = userRole;
            ViewBag.ExternalUnitId = externalUnitId;

            if (externalUnitId.HasValue)
            {
                var u = await _db.ExternalOrganizationalUnits
                    .FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                ViewBag.SelectedUnitName = u?.ArabicName ?? u?.ArabicUnitName;
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
                var grand = await _db.ExternalOrganizationalUnits
                    .Where(u => u.ParentId == childId && u.IsActive).Select(u => u.Id).ToListAsync();
                result.AddRange(grand);
            }
            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportingEntities()
        {
            var entities = await _db.SupportingEntities.Where(e => e.IsActive)
                .OrderBy(e => e.NameAr).Select(e => new { e.Id, e.NameAr }).ToListAsync();
            return Json(entities);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubObjectivesByUnit(int? externalUnitId)
        {
            if (!externalUnitId.HasValue) return Json(new List<object>());
            var list = await _db.SubObjectives
                .Where(s => s.ExternalUnitId == externalUnitId.Value && s.IsActive)
                .OrderBy(s => s.OrderIndex)
                .Select(s => new { id = s.Id, nameAr = s.NameAr, nameEn = s.NameEn })
                .ToListAsync();
            return Json(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Initiative).Include(p => p.ExternalUnit)
                .Include(p => p.ProjectManager).Include(p => p.CreatedBy)
                .Include(p => p.SubObjective).Include(p => p.FinancialCost)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return NotFound();
            if (!CanAccessProject(project)) return Forbid();

            var steps = await _db.Steps.Where(s => s.ProjectId == id && !s.IsDeleted)
                .Include(s => s.AssignedTo).OrderBy(s => s.StepNumber).ToListAsync();

            foreach (var step in steps)
                if (step.IsDelayed && step.Status != StepStatus.Delayed)
                    step.Status = StepStatus.Delayed;

            var requirements = await _db.ProjectRequirements.Where(r => r.ProjectId == id)
                .OrderBy(r => r.OrderIndex).ToListAsync();
            var kpis = await _db.ProjectKPIs.Where(k => k.ProjectId == id)
                .OrderBy(k => k.OrderIndex).ToListAsync();
            var supportingEntities = await _db.ProjectSupportingUnits.Where(s => s.ProjectId == id)
                .Include(s => s.SupportingEntity)
                .Select(s => new SupportingEntityDisplayItem
                {
                    Id = s.SupportingEntityId > 0 ? s.SupportingEntity!.Id : (s.ExternalUnitId ?? 0),
                    NameAr = s.ExternalUnitName ?? s.SupportingEntity!.NameAr ?? "",
                    NameEn = s.SupportingEntity != null ? s.SupportingEntity.NameEn ?? "" : "",
                    RepresentativeEmpNumber = s.RepresentativeEmpNumber,
                    RepresentativeName = s.RepresentativeName,
                    RepresentativeRank = s.RepresentativeRank
                }).ToListAsync();
            var yearTargets = await _db.ProjectYearTargets.Where(y => y.ProjectId == id)
                .OrderBy(y => y.Year).ToListAsync();
            var yearTargetItems = yearTargets.Select(y => new YearTargetDisplayItem
            {
                Id = y.Id,
                Year = y.Year,
                TargetPercentage = y.TargetPercentage,
                ActualPercentage = steps.Where(s => !s.IsDeleted && s.ProgressPercentage >= 100 &&
                    s.ActualEndDate.HasValue && s.ActualEndDate.Value.Year == y.Year).Sum(s => s.Weight),
                Notes = y.Notes
            }).ToList();

            var viewModel = new ProjectDetailsViewModel
            {
                Project = project,
                Steps = steps,
                Notes = await _db.ProgressUpdates.Where(p => p.ProjectId == id)
                    .Include(p => p.CreatedBy).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync(),
                Requirements = requirements,
                KPIs = kpis,
                SupportingEntities = supportingEntities,
                YearTargets = yearTargetItems
            };

            project.ProgressPercentage = viewModel.CalculatedProgress;
            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = GetCurrentUserRole();
            ViewBag.CurrentUserId = GetCurrentUserId();
            return View(viewModel);
        }

        public async Task<IActionResult> Create(int? initiativeId)
        {
            if (!CanEdit()) return Forbid();
            if (!initiativeId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب تحديد المبادرة لإضافة مشروع";
                return RedirectToAction("Index", "Initiatives");
            }
            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == initiativeId.Value && !i.IsDeleted);
            if (initiative == null) return NotFound();

            var viewModel = new ProjectFormViewModel
            {
                InitiativeId = initiativeId.Value,
                ExternalUnitId = initiative.ExternalUnitId,
                ExternalUnitName = initiative.ExternalUnitName
            };
            var currentYear = DateTime.Now.Year;
            var lastCode = await _db.Projects.Where(p => p.Code.StartsWith($"PRJ-{currentYear}"))
                .OrderByDescending(p => p.Code).Select(p => p.Code).FirstOrDefaultAsync();
            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var parts = lastCode.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int last)) nextNumber = last + 1;
            }
            viewModel.Code = $"PRJ-{currentYear}-{nextNumber:D3}";
            await PopulateFormDropdowns(viewModel);
            ViewBag.InitiativeName = initiative.NameAr;
            ViewBag.InitiativeCode = initiative.Code;
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            if (!CanEdit()) return Forbid();
            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == model.InitiativeId && !i.IsDeleted);
            if (initiative == null) return NotFound();

            if (ModelState.IsValid)
            {
                if (await _db.Projects.AnyAsync(p => p.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    ViewBag.InitiativeName = initiative.NameAr;
                    return View(model);
                }
                if (!string.IsNullOrWhiteSpace(model.ProjectNumber) &&
                    await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber && !p.IsDeleted))
                {
                    ModelState.AddModelError("ProjectNumber", "رقم المشروع مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    ViewBag.InitiativeName = initiative.NameAr;
                    return View(model);
                }

                var project = new Project
                {
                    CreatedById = GetCurrentUserId(),
                    CreatedAt = DateTime.Now,
                    ProgressPercentage = 0
                };
                model.UpdateEntity(project);
                _db.Projects.Add(project);
                await _db.SaveChangesAsync();
                await SaveRequirements(project.Id, model.Requirements);
                await SaveKPIs(project.Id, model.KPIItems);
                await SaveSupportingEntities(project.Id, model.SupportingEntityIds, model.SupportingEntitiesWithReps);
                await SaveYearTargets(project.Id, model.YearTargets);
                TempData["SuccessMessage"] = "تم إضافة المشروع بنجاح";
                return RedirectToAction("Details", "Initiatives", new { id = project.InitiativeId });
            }

            await PopulateFormDropdowns(model);
            ViewBag.InitiativeName = initiative.NameAr;
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit()) return Forbid();
            var project = await _db.Projects
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .Include(p => p.Requirements.OrderBy(r => r.OrderIndex))
                .Include(p => p.ProjectKPIs.OrderBy(k => k.OrderIndex))
                .Include(p => p.SupportingUnits).ThenInclude(s => s.SupportingEntity)
                .Include(p => p.YearTargets.OrderBy(y => y.Year))
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return NotFound();

            var viewModel = ProjectFormViewModel.FromEntity(project);
            await PopulateFormDropdowns(viewModel);
            ViewBag.InitiativeName = project.Initiative?.NameAr;
            ViewBag.InitiativeCode = project.Initiative?.Code;
            ViewBag.CalculatedProgress = project.Steps
                .Where(s => s.ProgressPercentage >= 100).Sum(s => s.Weight);
            return View(viewModel);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
        {
            if (!CanEdit()) return Forbid();
            if (id != model.Id) return NotFound();

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == model.InitiativeId);
            var calculatedProgress = await _db.Steps
                .Where(s => s.ProjectId == id && !s.IsDeleted && s.ProgressPercentage >= 100)
                .SumAsync(s => s.Weight);

            if (ModelState.IsValid)
            {
                var project = await _db.Projects
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                if (project == null) return NotFound();

                if (await _db.Projects.AnyAsync(p => p.Code == model.Code && p.Id != id))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    ViewBag.InitiativeName = initiative?.NameAr;
                    ViewBag.CalculatedProgress = calculatedProgress;
                    return View(model);
                }
                if (!string.IsNullOrWhiteSpace(model.ProjectNumber) &&
                    await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber &&
                        p.Id != id && !p.IsDeleted))
                {
                    ModelState.AddModelError("ProjectNumber", "رقم المشروع مستخدم بالفعل");
                    await PopulateFormDropdowns(model);
                    ViewBag.InitiativeName = initiative?.NameAr;
                    ViewBag.CalculatedProgress = calculatedProgress;
                    return View(model);
                }

                model.UpdateEntity(project);
                project.LastModifiedById = GetCurrentUserId();
                project.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                _db.ProjectRequirements.RemoveRange(
                    await _db.ProjectRequirements.Where(r => r.ProjectId == id).ToListAsync());
                _db.ProjectKPIs.RemoveRange(
                    await _db.ProjectKPIs.Where(k => k.ProjectId == id).ToListAsync());
                _db.ProjectSupportingUnits.RemoveRange(
                    await _db.ProjectSupportingUnits.Where(s => s.ProjectId == id).ToListAsync());
                _db.ProjectYearTargets.RemoveRange(
                    await _db.ProjectYearTargets.Where(y => y.ProjectId == id).ToListAsync());
                await _db.SaveChangesAsync();

                await SaveRequirements(project.Id, model.Requirements);
                await SaveKPIs(project.Id, model.KPIItems);
                await SaveSupportingEntities(project.Id, model.SupportingEntityIds, model.SupportingEntitiesWithReps);
                await SaveYearTargets(project.Id, model.YearTargets);
                TempData["SuccessMessage"] = "تم تحديث المشروع بنجاح";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }

            await PopulateFormDropdowns(model);
            ViewBag.InitiativeName = initiative?.NameAr;
            ViewBag.CalculatedProgress = calculatedProgress;
            return View(model);
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!CanEdit()) return Forbid();
            var project = await _db.Projects.Include(p => p.Initiative)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return NotFound();
            return View(project);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanEdit()) return Forbid();
            var project = await _db.Projects.FindAsync(id);
            if (project == null) return NotFound();
            project.IsDeleted = true;
            project.LastModifiedById = GetCurrentUserId();
            project.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المشروع بنجاح";
            return RedirectToAction("Details", "Initiatives", new { id = project.InitiativeId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id });
            }
            var project = await _db.Projects.FindAsync(id);
            if (project == null || project.IsDeleted) return NotFound();
            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                ProjectId = id,
                NotesAr = note,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            project.LastModifiedById = GetCurrentUserId();
            project.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int projectId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.ProjectId != projectId) return NotFound();
            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            note.NotesAr = notes;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تعديل الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int projectId)
        {
            if (GetCurrentUserRole() != UserRole.Admin) return Forbid();
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.ProjectId != projectId) return NotFound();
            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateProgress(int id)
        {
            var project = await _db.Projects.Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return NotFound();
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            if (!(userRole == UserRole.Admin ||
                  (userRole == UserRole.User && project.ProjectManagerId == userId)))
                return Forbid();
            var newProgress = CalculateProjectProgress(project);
            if (project.ProgressPercentage != newProgress)
            {
                project.ProgressPercentage = newProgress;
                project.LastModifiedById = userId;
                project.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        #region Helper Methods

        private decimal CalculateProjectProgress(Project project)
        {
            if (project.Steps == null || !project.Steps.Any()) return 0;
            var active = project.Steps.Where(s => !s.IsDeleted).ToList();
            if (!active.Any()) return 0;
            return active.Where(s => s.ProgressPercentage >= 100).Sum(s => s.Weight);
        }

        public async Task UpdateProjectProgressAsync(int projectId)
        {
            var project = await _db.Projects.Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project != null)
            {
                project.ProgressPercentage = CalculateProjectProgress(project);
                await _db.SaveChangesAsync();
            }
        }

        private bool CanAccessProject(Project project)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            return userRole switch
            {
                UserRole.Admin => true,
                UserRole.Executive => true,
                UserRole.Supervisor => _db.Initiatives.Any(i =>
                    i.Id == project.InitiativeId && i.SupervisorId == userId),
                UserRole.User => project.ProjectManagerId == userId,
                _ => false
            };
        }

        private async Task PopulateFilterDropdowns(ProjectListViewModel model)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            var q = _db.Initiatives.Where(i => !i.IsDeleted);
            if (userRole == UserRole.Supervisor)
                q = q.Where(i => i.SupervisorId == userId);
            model.Initiatives = new SelectList(
                await q.OrderBy(i => i.NameAr).ToListAsync(), "Id", "NameAr", model.InitiativeId);
        }

        private async Task PopulateFormDropdowns(ProjectFormViewModel model)
        {
            model.Initiatives = new SelectList(
                await _db.Initiatives.Where(i => !i.IsDeleted).OrderBy(i => i.NameAr).ToListAsync(),
                "Id", "NameAr", model.InitiativeId);
            model.ProjectManagers = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(),
                "Id", "FullNameAr", model.ProjectManagerId);
            model.FinancialCosts = new SelectList(
                await _db.FinancialCosts.Where(f => f.IsActive).OrderBy(f => f.OrderIndex)
                    .Select(f => new { f.Id, f.NameAr }).ToListAsync(),
                "Id", "NameAr", model.FinancialCostId);
            if (model.ExternalUnitId.HasValue)
                model.SubObjectives = new SelectList(
                    await _db.SubObjectives
                        .Where(s => s.ExternalUnitId == model.ExternalUnitId.Value && s.IsActive)
                        .OrderBy(s => s.OrderIndex).Select(s => new { s.Id, s.NameAr }).ToListAsync(),
                    "Id", "NameAr", model.SubObjectiveId);
            model.SubObjectives ??= new SelectList(Enumerable.Empty<SelectListItem>());
        }

        private async Task SaveRequirements(int projectId, List<string>? requirements)
        {
            if (requirements == null || !requirements.Any()) return;
            var entities = requirements.Where(r => !string.IsNullOrWhiteSpace(r))
                .Select((r, i) => new ProjectRequirement
                {
                    ProjectId = projectId,
                    RequirementText = r.Trim(),
                    OrderIndex = i,
                    CreatedAt = DateTime.Now
                }).ToList();
            if (entities.Any()) { _db.ProjectRequirements.AddRange(entities); await _db.SaveChangesAsync(); }
        }

        private async Task SaveKPIs(int projectId, List<KPIItemViewModel>? kpis)
        {
            if (kpis == null || !kpis.Any()) return;
            var entities = kpis.Where(k => !string.IsNullOrWhiteSpace(k.KPIText))
                .Select((k, i) => new ProjectKPI
                {
                    ProjectId = projectId,
                    KPIText = k.KPIText.Trim(),
                    TargetValue = k.TargetValue?.Trim(),
                    ActualValue = k.ActualValue?.Trim(),
                    OrderIndex = i,
                    CreatedAt = DateTime.Now
                }).ToList();
            if (entities.Any()) { _db.ProjectKPIs.AddRange(entities); await _db.SaveChangesAsync(); }
        }

        private async Task SaveSupportingEntities(int projectId,
            List<int>? localIds, List<SupportingEntityWithRepViewModel>? apiEntities)
        {
            if (apiEntities == null || !apiEntities.Any()) return;
            var units = apiEntities.Select(e => new ProjectSupportingUnit
            {
                ProjectId = projectId,
                ExternalUnitId = e.ExternalUnitId,
                ExternalUnitName = e.UnitName,
                RepresentativeEmpNumber = e.RepresentativeEmpNumber,
                RepresentativeName = e.RepresentativeName,
                RepresentativeRank = e.RepresentativeRank,
                CreatedAt = DateTime.Now
            }).ToList();
            if (units.Any()) { _db.ProjectSupportingUnits.AddRange(units); await _db.SaveChangesAsync(); }
        }

        private async Task SaveYearTargets(int projectId, List<YearTargetItemViewModel>? targets)
        {
            if (targets == null || !targets.Any()) return;
            var entities = targets.Where(y => y.TargetPercentage > 0)
                .Select(y => new ProjectYearTarget
                {
                    ProjectId = projectId,
                    Year = y.Year,
                    TargetPercentage = y.TargetPercentage,
                    Notes = y.Notes?.Trim(),
                    CreatedAt = DateTime.Now
                }).ToList();
            if (entities.Any()) { _db.ProjectYearTargets.AddRange(entities); await _db.SaveChangesAsync(); }
        }

        #endregion
    }
}