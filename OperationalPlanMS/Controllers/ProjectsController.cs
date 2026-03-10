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

        public ProjectsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Projects
        // تم تعديل الفلتر ليدعم ExternalUnitId من API الخارجي
        public async Task<IActionResult> Index(ProjectListViewModel model, int? externalUnitId)
        {
            var query = _db.Projects
                .Where(p => !p.IsDeleted)
                .Include(p => p.Initiative)
                .Include(p => p.ProjectManager)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            // Apply role-based filtering
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor)
            {
                var supervisedInitiativeIds = await _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id)
                    .ToListAsync();
                query = query.Where(p => supervisedInitiativeIds.Contains(p.InitiativeId));
            }
            else if (userRole == UserRole.User)
            {
                query = query.Where(p => p.ProjectManagerId == userId);
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                query = query.Where(p =>
                    p.NameAr.Contains(model.SearchTerm) ||
                    p.NameEn.Contains(model.SearchTerm) ||
                    p.Code.Contains(model.SearchTerm));
            }

            if (model.InitiativeId.HasValue)
            {
                query = query.Where(p => p.InitiativeId == model.InitiativeId.Value);
            }

            // ===== فلتر الوحدة التنظيمية من API الخارجي =====
            if (externalUnitId.HasValue)
            {
                // جلب الوحدة المختارة وجميع الوحدات الفرعية
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);

                // البحث في المشاريع عبر المبادرة
                query = query.Where(p =>
                    p.Initiative.ExternalUnitId.HasValue && unitIds.Contains(p.Initiative.ExternalUnitId.Value));
            }
            // فلتر الوحدة المحلية (للتوافقية)
            {
            }

            model.TotalCount = await query.CountAsync();

            model.Projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            // حساب نسبة الإنجاز لكل مشروع من الخطوات
            foreach (var project in model.Projects)
            {
                project.ProgressPercentage = CalculateProjectProgress(project);
            }

            await PopulateFilterDropdowns(model);

            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = userRole;

            // ===== تمرير ExternalUnitId للـ View =====
            ViewBag.ExternalUnitId = externalUnitId;

            // جلب اسم الوحدة المختارة للعرض
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

        /// <summary>
        /// API: جلب الوحدات التنظيمية لمنظمة معينة
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrganizationalUnits(int organizationId)
        {
            var units = await _db.OrganizationalUnits
                .Where(u => u.OrganizationId == organizationId && u.IsActive)
                .OrderBy(u => u.NameAr)
                .Select(u => new { u.Id, u.NameAr })
                .ToListAsync();

            return Json(units);
        }

        /// <summary>
        /// API: جلب جهات المساندة لمنظمة معينة
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSupportingEntities(int organizationId)
        {
            var entities = await _db.SupportingEntities
                .Where(e => e.OrganizationId == organizationId && e.IsActive)
                .OrderBy(e => e.NameAr)
                .Select(e => new { e.Id, e.NameAr })
                .ToListAsync();

            return Json(entities);
        }

        /// <summary>
        /// API: جلب معلومات جهة مساندة واحدة (للتحميل المسبق)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSupportingEntityInfo(int id)
        {
            var entity = await _db.SupportingEntities
                .Where(e => e.Id == id)
                .Select(e => new { e.Id, e.NameAr })
                .FirstOrDefaultAsync();

            if (entity == null)
                return NotFound();

            return Json(entity);
        }

        /// <summary>
        /// API: جلب الأهداف الفرعية حسب اسم الوحدة التنظيمية
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSubObjectivesByUnit(string unitName)
        {
            if (string.IsNullOrEmpty(unitName))
            {
                return Json(new List<object>());
            }

                .FirstOrDefaultAsync(u => u.NameAr == unitName || u.NameEn == unitName);

            if (localUnit == null)
            {
                return Json(new List<object>());
            }

            var subObjectives = await _db.SubObjectives
                .OrderBy(s => s.OrderIndex)
                .Select(s => new
                {
                    id = s.Id,
                    nameAr = s.NameAr,
                    nameEn = s.NameEn
                })
                .ToListAsync();

            return Json(subObjectives);
        }

        // GET: /Projects/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Initiative)
                .Include(p => p.ExternalUnit)
                .Include(p => p.ProjectManager)
                .Include(p => p.CreatedBy)
                .Include(p => p.SubObjective)
                .Include(p => p.FinancialCost)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }

            if (!CanAccessProject(project))
            {
                return Forbid();
            }

            var steps = await _db.Steps
                .Where(s => s.ProjectId == id && !s.IsDeleted)
                .Include(s => s.AssignedTo)
                .OrderBy(s => s.StepNumber)
                .ToListAsync();

            foreach (var step in steps)
            {
                if (step.IsDelayed && step.Status != StepStatus.Delayed)
                {
                    step.Status = StepStatus.Delayed;
                }
            }

            var requirements = await _db.ProjectRequirements
                .Where(r => r.ProjectId == id)
                .OrderBy(r => r.OrderIndex)
                .ToListAsync();

            var kpis = await _db.ProjectKPIs
                .Where(k => k.ProjectId == id)
                .OrderBy(k => k.OrderIndex)
                .ToListAsync();

            var supportingEntities = await _db.ProjectSupportingUnits
                .Where(s => s.ProjectId == id)
                .Include(s => s.SupportingEntity)
                .Select(s => new SupportingEntityDisplayItem
                {
                    Id = s.SupportingEntityId > 0 ? s.SupportingEntity!.Id : (s.ExternalUnitId ?? 0),
                    NameAr = s.ExternalUnitName ?? s.SupportingEntity!.NameAr ?? "",
                    NameEn = s.SupportingEntity != null ? s.SupportingEntity.NameEn ?? "" : "",
                    RepresentativeEmpNumber = s.RepresentativeEmpNumber,
                    RepresentativeName = s.RepresentativeName,
                    RepresentativeRank = s.RepresentativeRank
                })
                .ToListAsync();

            var yearTargets = await _db.ProjectYearTargets
                .Where(y => y.ProjectId == id)
                .OrderBy(y => y.Year)
                .ToListAsync();

            var yearTargetDisplayItems = yearTargets.Select(y => new YearTargetDisplayItem
            {
                Id = y.Id,
                Year = y.Year,
                TargetPercentage = y.TargetPercentage,
                ActualPercentage = steps
                    .Where(s => !s.IsDeleted &&
                                s.ProgressPercentage >= 100 &&
                                s.ActualEndDate.HasValue &&
                                s.ActualEndDate.Value.Year == y.Year)
                    .Sum(s => s.Weight),
                Notes = y.Notes
            }).ToList();

            var viewModel = new ProjectDetailsViewModel
            {
                Project = project,
                Steps = steps,
                Notes = await _db.ProgressUpdates
                    .Where(p => p.ProjectId == id)
                    .Include(p => p.CreatedBy)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(20)
                    .ToListAsync(),
                Requirements = requirements,
                KPIs = kpis,
                SupportingEntities = supportingEntities,
                YearTargets = yearTargetDisplayItems
            };

            project.ProgressPercentage = viewModel.CalculatedProgress;

            var userRole = GetCurrentUserRole();
            var currentUserId = GetCurrentUserId();

            ViewBag.CanEdit = CanEdit();
            ViewBag.UserRole = userRole;
            ViewBag.CurrentUserId = currentUserId;

            return View(viewModel);
        }

        // GET: /Projects/Create?initiativeId=5
        public async Task<IActionResult> Create(int? initiativeId)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            if (!initiativeId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب تحديد المبادرة لإضافة مشروع";
                return RedirectToAction("Index", "Initiatives");
            }

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == initiativeId.Value && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }


            var viewModel = new ProjectFormViewModel
            {
                InitiativeId = initiativeId.Value,
                OrganizationId = organizationId,
            };

            var currentYear = DateTime.Now.Year;
            var lastCode = await _db.Projects
                .Where(p => p.Code.StartsWith($"PRJ-{currentYear}"))
                .OrderByDescending(p => p.Code)
                .Select(p => p.Code)
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
            viewModel.Code = $"PRJ-{currentYear}-{nextNumber:D3}";

            await PopulateFormDropdowns(viewModel, organizationId);

                .Where(o => o.IsActive)
                .OrderBy(o => o.NameAr)
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.NameAr })
                .ToListAsync();

            ViewBag.InitiativeName = initiative.NameAr;
            ViewBag.InitiativeCode = initiative.Code;

            return View(viewModel);
        }

        // POST: /Projects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == model.InitiativeId && !i.IsDeleted);

            if (initiative == null)
            {
                return NotFound();
            }


            if (ModelState.IsValid)
            {
                if (await _db.Projects.AnyAsync(p => p.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model, organizationId);
                    SetViewBagForCreate(initiative, organizationId);
                    return View(model);
                }

                if (!string.IsNullOrWhiteSpace(model.ProjectNumber))
                {
                    if (await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber && !p.IsDeleted))
                    {
                        ModelState.AddModelError("ProjectNumber", "رقم المشروع مستخدم بالفعل");
                        await PopulateFormDropdowns(model, organizationId);
                        SetViewBagForCreate(initiative, organizationId);
                            .Where(o => o.IsActive)
                            .OrderBy(o => o.NameAr)
                            .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.NameAr })
                            .ToListAsync();
                        return View(model);
                    }
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

            await PopulateFormDropdowns(model, organizationId);
            SetViewBagForCreate(initiative, organizationId);
            return View(model);
        }

        // GET: /Projects/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var project = await _db.Projects
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .Include(p => p.Requirements.OrderBy(r => r.OrderIndex))
                .Include(p => p.ProjectKPIs.OrderBy(k => k.OrderIndex))
                .Include(p => p.SupportingUnits)
                    .ThenInclude(s => s.SupportingEntity)
                .Include(p => p.YearTargets.OrderBy(y => y.Year))
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }


            var viewModel = ProjectFormViewModel.FromEntity(project);

            await PopulateFormDropdowns(viewModel, organizationId);

                .Where(o => o.IsActive)
                .OrderBy(o => o.NameAr)
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.NameAr })
                .ToListAsync();

            ViewBag.InitiativeName = project.Initiative?.NameAr;
            ViewBag.InitiativeCode = project.Initiative?.Code;
            ViewBag.CalculatedProgress = project.Steps.Where(s => s.ProgressPercentage >= 100).Sum(s => s.Weight);

            return View(viewModel);
        }

        // POST: /Projects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            var initiative = await _db.Initiatives
                .FirstOrDefaultAsync(i => i.Id == model.InitiativeId);


            var calculatedProgress = await _db.Steps
                .Where(s => s.ProjectId == id && !s.IsDeleted && s.ProgressPercentage >= 100)
                .SumAsync(s => s.Weight);

            if (ModelState.IsValid)
            {
                var project = await _db.Projects
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (project == null)
                {
                    return NotFound();
                }

                if (await _db.Projects.AnyAsync(p => p.Code == model.Code && p.Id != id))
                {
                    ModelState.AddModelError("Code", "هذا الكود مستخدم بالفعل");
                    await PopulateFormDropdowns(model, organizationId);
                    SetViewBagForEdit(initiative, organizationId);
                    ViewBag.CalculatedProgress = calculatedProgress;
                    return View(model);
                }

                if (!string.IsNullOrWhiteSpace(model.ProjectNumber))
                {
                    if (await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber && p.Id != id && !p.IsDeleted))
                    {
                        ModelState.AddModelError("ProjectNumber", "رقم المشروع مستخدم بالفعل");
                        await PopulateFormDropdowns(model, organizationId);
                        SetViewBagForEdit(initiative, organizationId);
                        ViewBag.CalculatedProgress = calculatedProgress;
                            .Where(o => o.IsActive)
                            .OrderBy(o => o.NameAr)
                            .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.NameAr })
                            .ToListAsync();
                        return View(model);
                    }
                }

                model.UpdateEntity(project);
                project.LastModifiedById = GetCurrentUserId();
                project.LastModifiedAt = DateTime.Now;

                await _db.SaveChangesAsync();

                var oldRequirements = await _db.ProjectRequirements.Where(r => r.ProjectId == id).ToListAsync();
                _db.ProjectRequirements.RemoveRange(oldRequirements);

                var oldKPIs = await _db.ProjectKPIs.Where(k => k.ProjectId == id).ToListAsync();
                _db.ProjectKPIs.RemoveRange(oldKPIs);

                var oldUnits = await _db.ProjectSupportingUnits.Where(s => s.ProjectId == id).ToListAsync();
                _db.ProjectSupportingUnits.RemoveRange(oldUnits);

                var oldYears = await _db.ProjectYearTargets.Where(y => y.ProjectId == id).ToListAsync();
                _db.ProjectYearTargets.RemoveRange(oldYears);

                await _db.SaveChangesAsync();

                await SaveRequirements(project.Id, model.Requirements);
                await SaveKPIs(project.Id, model.KPIItems);
                await SaveSupportingEntities(project.Id, model.SupportingEntityIds, model.SupportingEntitiesWithReps);
                await SaveYearTargets(project.Id, model.YearTargets);

                TempData["SuccessMessage"] = "تم تحديث المشروع بنجاح";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }

            await PopulateFormDropdowns(model, organizationId);
            SetViewBagForEdit(initiative, organizationId);
            ViewBag.CalculatedProgress = calculatedProgress;
            return View(model);
        }

        // GET: /Projects/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var project = await _db.Projects
                .Include(p => p.Initiative)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // POST: /Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanEdit())
            {
                return Forbid();
            }

            var project = await _db.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            project.IsDeleted = true;
            project.LastModifiedById = GetCurrentUserId();
            project.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف المشروع بنجاح";
            return RedirectToAction("Details", "Initiatives", new { id = project.InitiativeId });
        }

        // POST: /Projects/AddNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id });
            }

            var project = await _db.Projects.FindAsync(id);
            if (project == null || project.IsDeleted)
            {
                return NotFound();
            }

            var progressUpdate = new ProgressUpdate
            {
                ProjectId = id,
                NotesAr = note,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };

            _db.ProgressUpdates.Add(progressUpdate);

            project.LastModifiedById = GetCurrentUserId();
            project.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Projects/EditNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int projectId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var noteEntity = await _db.ProgressUpdates.FindAsync(noteId);
            if (noteEntity == null || noteEntity.ProjectId != projectId)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            noteEntity.NotesAr = notes;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعديل الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: /Projects/DeleteNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int projectId)
        {
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var noteEntity = await _db.ProgressUpdates.FindAsync(noteId);
            if (noteEntity == null || noteEntity.ProjectId != projectId)
            {
                return NotFound();
            }

            _db.ProgressUpdates.Remove(noteEntity);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: /Projects/RecalculateProgress/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateProgress(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();
            var canRecalculate = userRole == UserRole.Admin ||
                                (userRole == UserRole.User && project.ProjectManagerId == userId);

            if (!canRecalculate)
            {
                return Forbid();
            }

            var newProgress = CalculateProjectProgress(project);

            if (project.ProgressPercentage != newProgress)
            {
                project.ProgressPercentage = newProgress;
                project.LastModifiedById = GetCurrentUserId();
                project.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        #region Helper Methods

        private decimal CalculateProjectProgress(Project project)
        {
            if (project.Steps == null || !project.Steps.Any())
                return 0;

            var activeSteps = project.Steps.Where(s => !s.IsDeleted).ToList();
            if (!activeSteps.Any())
                return 0;

            return activeSteps
                .Where(s => s.ProgressPercentage >= 100)
                .Sum(s => s.Weight);
        }

        public async Task UpdateProjectProgressAsync(int projectId)
        {
            var project = await _db.Projects
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
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
                UserRole.Supervisor => _db.Initiatives.Any(i => i.Id == project.InitiativeId && i.SupervisorId == userId),
                UserRole.User => project.ProjectManagerId == userId,
                _ => false
            };
        }

        private void SetViewBagForCreate(Initiative initiative, int organizationId)
        {
            ViewBag.InitiativeName = initiative.NameAr;
            ViewBag.InitiativeCode = initiative.Code;
        }

        private void SetViewBagForEdit(Initiative? initiative, int organizationId)
        {
            ViewBag.InitiativeName = initiative?.NameAr;
            ViewBag.InitiativeCode = initiative?.Code;
        }

        private async Task PopulateFilterDropdowns(ProjectListViewModel model)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var initiativesQuery = _db.Initiatives.Where(i => !i.IsDeleted);
            if (userRole == UserRole.Supervisor)
            {
                initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId);
            }

            model.Initiatives = new SelectList(
                await initiativesQuery.OrderBy(i => i.NameAr).ToListAsync(),
                "Id", "NameAr", model.InitiativeId);

            model.OrganizationalUnits = new SelectList(
        }

        private async Task PopulateFormDropdowns(ProjectFormViewModel model, int organizationId = 0)
        {
            model.Initiatives = new SelectList(
                await _db.Initiatives.Where(i => !i.IsDeleted).OrderBy(i => i.NameAr).ToListAsync(),
                "Id", "NameAr", model.InitiativeId);

            if (organizationId > 0)
            {
                model.OrganizationalUnits = new SelectList(
                    await _db.OrganizationalUnits
                        .Where(u => u.IsActive && u.OrganizationId == organizationId)
                        .ToListAsync(),
            }
            else
            {
                model.OrganizationalUnits = new SelectList(
            }

            model.ProjectManagers = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(),
                "Id", "FullNameAr", model.ProjectManagerId);

            model.FinancialCosts = new SelectList(
                await _db.FinancialCosts
                    .Where(f => f.IsActive)
                    .OrderBy(f => f.OrderIndex)
                    .Select(f => new { f.Id, f.NameAr })
                    .ToListAsync(),
                "Id", "NameAr", model.FinancialCostId);

            if (!string.IsNullOrEmpty(model.ExternalUnitName))
            {
                    .FirstOrDefaultAsync(u => u.NameAr == model.ExternalUnitName || u.NameEn == model.ExternalUnitName);

                if (localUnit != null)
                {
                    model.SubObjectives = new SelectList(
                        await _db.SubObjectives
                            .OrderBy(s => s.OrderIndex)
                            .Select(s => new { s.Id, s.NameAr })
                            .ToListAsync(),
                        "Id", "NameAr", model.SubObjectiveId);
                }
            }

            model.SubObjectives ??= new SelectList(Enumerable.Empty<SelectListItem>());
        }

        private async Task SaveRequirements(int projectId, List<string>? requirements)
        {
            if (requirements == null || !requirements.Any()) return;

            var entities = requirements
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select((r, index) => new ProjectRequirement
                {
                    ProjectId = projectId,
                    RequirementText = r.Trim(),
                    OrderIndex = index,
                    CreatedAt = DateTime.Now
                })
                .ToList();

            if (entities.Any())
            {
                _db.ProjectRequirements.AddRange(entities);
                await _db.SaveChangesAsync();
            }
        }

        private async Task SaveKPIs(int projectId, List<KPIItemViewModel>? kpis)
        {
            if (kpis == null || !kpis.Any()) return;

            var entities = kpis
                .Where(k => !string.IsNullOrWhiteSpace(k.KPIText))
                .Select((k, index) => new ProjectKPI
                {
                    ProjectId = projectId,
                    KPIText = k.KPIText.Trim(),
                    TargetValue = k.TargetValue?.Trim(),
                    ActualValue = k.ActualValue?.Trim(),
                    OrderIndex = index,
                    CreatedAt = DateTime.Now
                })
                .ToList();

            if (entities.Any())
            {
                _db.ProjectKPIs.AddRange(entities);
                await _db.SaveChangesAsync();
            }
        }

        private async Task SaveSupportingEntities(int projectId, List<int>? localIds, List<SupportingEntityWithRepViewModel>? apiEntities)
        {
            if (apiEntities != null && apiEntities.Any())
            {
                var supportingUnits = apiEntities.Select(e => new ProjectSupportingUnit
                {
                    ProjectId = projectId,
                    ExternalUnitId = e.ExternalUnitId,
                    ExternalUnitName = e.UnitName,
                    RepresentativeEmpNumber = e.RepresentativeEmpNumber,
                    RepresentativeName = e.RepresentativeName,
                    RepresentativeRank = e.RepresentativeRank,
                    CreatedAt = DateTime.Now
                }).ToList();

                if (supportingUnits.Any())
                {
                    _db.ProjectSupportingUnits.AddRange(supportingUnits);
                    await _db.SaveChangesAsync();
                }
            }
        }

        private async Task SaveYearTargets(int projectId, List<YearTargetItemViewModel>? targets)
        {
            if (targets == null || !targets.Any()) return;

            var entities = targets
                .Where(y => y.TargetPercentage > 0)
                .Select(y => new ProjectYearTarget
                {
                    ProjectId = projectId,
                    Year = y.Year,
                    TargetPercentage = y.TargetPercentage,
                    Notes = y.Notes?.Trim(),
                    CreatedAt = DateTime.Now
                })
                .ToList();

            if (entities.Any())
            {
                _db.ProjectYearTargets.AddRange(entities);
                await _db.SaveChangesAsync();
            }
        }

        #endregion
    }
}