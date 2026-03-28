using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Services
{
    public interface IProjectService
    {
        // القراءة
        Task<ProjectListViewModel> GetListAsync(string? searchTerm, int? initiativeId, Guid? externalUnitId, int page, int pageSize, UserRole userRole, int userId);
        Task<ProjectDetailsViewModel?> GetDetailsAsync(int id);
        Task<Project?> GetByIdAsync(int id);
        Task<Project?> GetWithInitiativeAsync(int id);

        // الإنشاء والتعديل
        Task<ProjectFormViewModel?> PrepareCreateViewModelAsync(int initiativeId);
        Task<ProjectFormViewModel?> PrepareEditViewModelAsync(int id);
        Task<(bool Success, int? InitiativeId, string? Error, string? Warning)> CreateAsync(ProjectFormViewModel model, int createdById);
        Task<(bool Success, string? Error, string? Warning)> UpdateAsync(int id, ProjectFormViewModel model, int modifiedById);
        Task<(bool Success, string? Error)> SoftDeleteAsync(int id, int modifiedById);

        // الملاحظات
        Task<(bool Success, string? Error)> AddNoteAsync(int projectId, string note, int createdById);
        Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int projectId, string notes);
        Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int projectId);

        // إعادة حساب التقدم
        Task<decimal> RecalculateProgressAsync(int projectId);
        Task UpdateProjectProgressAsync(int projectId);

        // المساعدة
        Task PopulateFormDropdownsAsync(ProjectFormViewModel model);
        Task PopulateFilterDropdownsAsync(ProjectListViewModel model, UserRole userRole, int userId);
        bool CanAccess(Project project, UserRole userRole, int userId);
        Task<decimal> GetCalculatedProgressAsync(int projectId);

        // API Endpoints
        Task<object> GetSupportingEntitiesAsync();
        Task<object?> GetSupportingEntityInfoAsync(int id);
        Task<object> GetSubObjectivesByUnitAsync(Guid? externalUnitId);

        // معلومات الوحدة
        Task<string?> GetUnitNameAsync(Guid externalUnitId);
    }

    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(AppDbContext db, ILogger<ProjectService> logger)
        {
            _db = db;
            _logger = logger;
        }

        #region القراءة

        public async Task<ProjectListViewModel> GetListAsync(
            string? searchTerm, int? initiativeId, Guid? externalUnitId,
            int page, int pageSize, UserRole userRole, int userId)
        {
            var query = _db.Projects.Where(p => !p.IsDeleted)
                .Include(p => p.Initiative).Include(p => p.ProjectManager)
                .Include(p => p.Steps.Where(s => !s.IsDeleted)).AsQueryable();

            // تصفية حسب الدور
            if (userRole == UserRole.Supervisor)
            {
                var supervisedInitiativeIds = await _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted).Select(i => i.Id).ToListAsync();
                var accessibleInitiativeIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                var allIds = supervisedInitiativeIds.Union(accessibleInitiativeIds).ToList();
                query = query.Where(p => allIds.Contains(p.InitiativeId) || p.ProjectManagerId == userId);
            }
            else if (userRole == UserRole.User)
            {
                var accessibleInitiativeIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                query = query.Where(p => p.ProjectManagerId == userId || accessibleInitiativeIds.Contains(p.InitiativeId));
            }
            else if (userRole == UserRole.StepUser)
            {
                var myProjectIds = await _db.Steps.Where(s => !s.IsDeleted && s.AssignedToId == userId)
                    .Select(s => s.ProjectId).Distinct().ToListAsync();
                var accessibleInitiativeIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                query = query.Where(p => myProjectIds.Contains(p.Id) || accessibleInitiativeIds.Contains(p.InitiativeId));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(p => p.NameAr.Contains(searchTerm) || p.NameEn.Contains(searchTerm) || p.Code.Contains(searchTerm));
            if (initiativeId.HasValue)
                query = query.Where(p => p.InitiativeId == initiativeId.Value);
            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIdsAsync(externalUnitId.Value);
                query = query.Where(p => p.Initiative.ExternalUnitId.HasValue && unitIds.Contains(p.Initiative.ExternalUnitId.Value));
            }

            var totalCount = await query.CountAsync();
            var projects = await query.OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            foreach (var project in projects)
                project.ProgressPercentage = CalculateProjectProgress(project);

            var model = new ProjectListViewModel
            {
                Projects = projects,
                SearchTerm = searchTerm,
                InitiativeId = initiativeId,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            await PopulateFilterDropdownsAsync(model, userRole, userId);
            return model;
        }

        public async Task<ProjectDetailsViewModel?> GetDetailsAsync(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Initiative).Include(p => p.ExternalUnit)
                .Include(p => p.ProjectManager).Include(p => p.CreatedBy)
                .Include(p => p.SubObjective).Include(p => p.FinancialCost)
                .Include(p => p.ProjectSubObjectives).ThenInclude(ps => ps.SubObjective)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return null;

            var steps = await _db.Steps.Where(s => s.ProjectId == id && !s.IsDeleted)
                .Include(s => s.AssignedTo).OrderBy(s => s.StepNumber).ToListAsync();
            foreach (var step in steps)
                if (step.IsDelayed && step.Status != StepStatus.Delayed) step.Status = StepStatus.Delayed;

            var requirements = await _db.ProjectRequirements.Where(r => r.ProjectId == id).OrderBy(r => r.OrderIndex).ToListAsync();
            var kpis = await _db.ProjectKPIs.Where(k => k.ProjectId == id).OrderBy(k => k.OrderIndex).ToListAsync();

            // جهات المساندة مع الممثلين المتعددين
            var supportingUnits = await _db.ProjectSupportingUnits
                .Where(s => s.ProjectId == id)
                .Include(s => s.SupportingEntity)
                .Include(s => s.Representatives)
                .ToListAsync();

            var supportingEntities = supportingUnits.Select(s => new SupportingEntityDisplayItem
            {
                Id = s.SupportingEntityId > 0 ? s.SupportingEntity!.Id.ToString() : (s.ExternalUnitId?.ToString() ?? ""),
                NameAr = s.ExternalUnitName ?? s.SupportingEntity?.NameAr ?? "",
                NameEn = s.SupportingEntity != null ? s.SupportingEntity.NameEn ?? "" : "",
                // ممثلين متعددين (جديد)
                Representatives = s.Representatives.OrderBy(r => r.OrderIndex).Select(r => new RepresentativeViewModel
                {
                    EmpNumber = r.EmpNumber,
                    Name = r.Name,
                    Rank = r.Rank
                }).ToList(),
                // القديم (backward compat)
                RepresentativeEmpNumber = s.RepresentativeEmpNumber,
                RepresentativeName = s.RepresentativeName,
                RepresentativeRank = s.RepresentativeRank
            }).ToList();

            var yearTargets = await _db.ProjectYearTargets.Where(y => y.ProjectId == id).OrderBy(y => y.Year).ToListAsync();
            var yearTargetDisplayItems = yearTargets.Select(y => new YearTargetDisplayItem
            {
                Id = y.Id, Year = y.Year, TargetPercentage = y.TargetPercentage,
                ActualPercentage = steps.Where(s => !s.IsDeleted && s.ProgressPercentage >= 100 &&
                    s.ActualEndDate.HasValue && s.ActualEndDate.Value.Year == y.Year).Sum(s => s.Weight),
                Notes = y.Notes
            }).ToList();

            var viewModel = new ProjectDetailsViewModel
            {
                Project = project, Steps = steps,
                Notes = await _db.ProgressUpdates.Where(p => p.ProjectId == id)
                    .Include(p => p.CreatedBy).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync(),
                Requirements = requirements, KPIs = kpis,
                SupportingEntities = supportingEntities, YearTargets = yearTargetDisplayItems
            };
            project.ProgressPercentage = viewModel.CalculatedProgress;
            return viewModel;
        }

        public async Task<Project?> GetByIdAsync(int id) =>
            await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        public async Task<Project?> GetWithInitiativeAsync(int id) =>
            await _db.Projects.Include(p => p.Initiative).FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        #endregion

        #region الإنشاء والتعديل

        public async Task<ProjectFormViewModel?> PrepareCreateViewModelAsync(int initiativeId)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == initiativeId && !i.IsDeleted);
            if (initiative == null) return null;

            var viewModel = new ProjectFormViewModel
            {
                InitiativeId = initiativeId,
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
            await PopulateFormDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<ProjectFormViewModel?> PrepareEditViewModelAsync(int id)
        {
            var project = await _db.Projects
                .Include(p => p.Initiative).Include(p => p.Steps.Where(s => !s.IsDeleted))
                .Include(p => p.Requirements.OrderBy(r => r.OrderIndex))
                .Include(p => p.ProjectKPIs.OrderBy(k => k.OrderIndex))
                .Include(p => p.SupportingUnits).ThenInclude(s => s.SupportingEntity)
                .Include(p => p.SupportingUnits).ThenInclude(s => s.Representatives)
                .Include(p => p.YearTargets.OrderBy(y => y.Year))
                .Include(p => p.ProjectSubObjectives)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return null;

            var viewModel = ProjectFormViewModel.FromEntity(project);
            await PopulateFormDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<(bool Success, int? InitiativeId, string? Error, string? Warning)> CreateAsync(
            ProjectFormViewModel model, int createdById)
        {
            if (await _db.Projects.AnyAsync(p => p.Code == model.Code))
                return (false, model.InitiativeId, "هذا الكود مستخدم بالفعل", null);

            if (!string.IsNullOrWhiteSpace(model.ProjectNumber) &&
                await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber && !p.IsDeleted))
                return (false, model.InitiativeId, "رقم المشروع مستخدم بالفعل", null);

            var project = new Project { CreatedById = createdById, CreatedAt = DateTime.Now, ProgressPercentage = 0 };
            model.UpdateEntity(project);
            project.ProjectManagerId = await ResolveProjectManagerIdAsync(model.ProjectManagerEmpNumber);

            string? warning = null;
            if (!string.IsNullOrWhiteSpace(model.ProjectManagerEmpNumber) && project.ProjectManagerId == null)
                warning = $"تنبيه: مدير المشروع ({model.ProjectManagerName}) غير مسجّل في النظام.";

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            await SaveRelatedDataAsync(project.Id, model);
            _logger.LogInformation("تم إنشاء مشروع: {Code}", project.Code);
            return (true, project.InitiativeId, null, warning);
        }

        public async Task<(bool Success, string? Error, string? Warning)> UpdateAsync(
            int id, ProjectFormViewModel model, int modifiedById)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return (false, "المشروع غير موجود", null);

            if (await _db.Projects.AnyAsync(p => p.Code == model.Code && p.Id != id))
                return (false, "هذا الكود مستخدم بالفعل", null);
            if (!string.IsNullOrWhiteSpace(model.ProjectNumber) &&
                await _db.Projects.AnyAsync(p => p.ProjectNumber == model.ProjectNumber && p.Id != id && !p.IsDeleted))
                return (false, "رقم المشروع مستخدم بالفعل", null);

            model.UpdateEntity(project);
            project.ProjectManagerId = await ResolveProjectManagerIdAsync(model.ProjectManagerEmpNumber);

            string? warning = null;
            if (!string.IsNullOrWhiteSpace(model.ProjectManagerEmpNumber) && project.ProjectManagerId == null)
                warning = $"تنبيه: مدير المشروع ({model.ProjectManagerName}) غير مسجّل في النظام.";

            project.LastModifiedById = modifiedById;
            project.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            // حذف البيانات القديمة وإعادة حفظها
            // الممثلين يُحذفون تلقائياً مع الجهات المساندة (Cascade)
            _db.ProjectRequirements.RemoveRange(await _db.ProjectRequirements.Where(r => r.ProjectId == id).ToListAsync());
            _db.ProjectKPIs.RemoveRange(await _db.ProjectKPIs.Where(k => k.ProjectId == id).ToListAsync());

            // حذف الممثلين أولاً ثم الجهات المساندة
            var existingUnits = await _db.ProjectSupportingUnits.Where(s => s.ProjectId == id).Include(s => s.Representatives).ToListAsync();
            foreach (var unit in existingUnits)
            {
                _db.SupportingUnitRepresentatives.RemoveRange(unit.Representatives);
            }
            _db.ProjectSupportingUnits.RemoveRange(existingUnits);

            _db.ProjectYearTargets.RemoveRange(await _db.ProjectYearTargets.Where(y => y.ProjectId == id).ToListAsync());
            _db.ProjectSubObjectives.RemoveRange(await _db.ProjectSubObjectives.Where(ps => ps.ProjectId == id).ToListAsync());
            await _db.SaveChangesAsync();

            await SaveRelatedDataAsync(project.Id, model);
            _logger.LogInformation("تم تحديث مشروع: {Code}", project.Code);
            return (true, null, warning);
        }

        public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id, int modifiedById)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (project == null) return (false, "المشروع غير موجود");
            project.IsDeleted = true;
            project.LastModifiedById = modifiedById;
            project.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            _logger.LogInformation("تم حذف مشروع: {Code}", project.Code);
            return (true, null);
        }

        #endregion

        #region الملاحظات

        public async Task<(bool Success, string? Error)> AddNoteAsync(int projectId, string note, int createdById)
        {
            if (string.IsNullOrWhiteSpace(note)) return (false, "الملاحظة مطلوبة");
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null || project.IsDeleted) return (false, "المشروع غير موجود");
            _db.ProgressUpdates.Add(new ProgressUpdate { ProjectId = projectId, NotesAr = note, CreatedById = createdById, CreatedAt = DateTime.Now });
            project.LastModifiedById = createdById; project.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int projectId, string notes)
        {
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.ProjectId != projectId) return (false, "الملاحظة غير موجودة");
            if (string.IsNullOrWhiteSpace(notes)) return (false, "الملاحظة مطلوبة");
            note.NotesAr = notes;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int projectId)
        {
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.ProjectId != projectId) return (false, "الملاحظة غير موجودة");
            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        #endregion

        #region التقدم

        public async Task<decimal> RecalculateProgressAsync(int projectId)
        {
            var project = await _db.Projects.Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project == null) return 0;
            var newProgress = CalculateProjectProgress(project);
            if (project.ProgressPercentage != newProgress)
            {
                project.ProgressPercentage = newProgress;
                project.LastModifiedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            return newProgress;
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

        public async Task<decimal> GetCalculatedProgressAsync(int projectId)
        {
            return await _db.Steps.Where(s => s.ProjectId == projectId && !s.IsDeleted && s.ProgressPercentage >= 100)
                .SumAsync(s => s.Weight);
        }

        #endregion

        #region API

        public async Task<object> GetSupportingEntitiesAsync()
        {
            return await _db.SupportingEntities.Where(e => e.IsActive)
                .OrderBy(e => e.NameAr).Select(e => new { e.Id, e.NameAr }).ToListAsync();
        }

        public async Task<object?> GetSupportingEntityInfoAsync(int id)
        {
            return await _db.SupportingEntities.Where(e => e.Id == id)
                .Select(e => new { e.Id, e.NameAr }).FirstOrDefaultAsync();
        }

        public async Task<object> GetSubObjectivesByUnitAsync(Guid? externalUnitId)
        {
            if (!externalUnitId.HasValue) return new List<object>();
            return await _db.SubObjectives.Where(s => s.ExternalUnitId == externalUnitId.Value && s.IsActive)
                .OrderBy(s => s.OrderIndex).Select(s => new { id = s.Id, nameAr = s.NameAr, nameEn = s.NameEn }).ToListAsync();
        }

        public async Task<string?> GetUnitNameAsync(Guid externalUnitId)
        {
            var unit = await _db.ExternalOrganizationalUnits.FirstOrDefaultAsync(u => u.Id == externalUnitId);
            return unit?.ArabicName ?? unit?.ArabicUnitName;
        }

        #endregion

        #region المساعدة

        public bool CanAccess(Project project, UserRole userRole, int userId)
        {
            if (userRole == UserRole.Admin || userRole == UserRole.Executive)
                return true;

            // Role-based checks
            if (userRole == UserRole.Supervisor &&
                (_db.Initiatives.Any(i => i.Id == project.InitiativeId && i.SupervisorId == userId)
                 || project.ProjectManagerId == userId))
                return true;

            if (userRole == UserRole.User && project.ProjectManagerId == userId)
                return true;

            if (userRole == UserRole.StepUser &&
                _db.Steps.Any(s => s.ProjectId == project.Id && s.AssignedToId == userId && !s.IsDeleted))
                return true;

            // Fallback: check InitiativeAccess for ALL roles
            return _db.InitiativeAccess.Any(a =>
                a.InitiativeId == project.InitiativeId &&
                a.UserId == userId &&
                a.IsActive);
        }

        public async Task PopulateFormDropdownsAsync(ProjectFormViewModel model)
        {
            model.Initiatives = new SelectList(await _db.Initiatives.Where(i => !i.IsDeleted).OrderBy(i => i.NameAr).ToListAsync(), "Id", "NameAr", model.InitiativeId);
            model.ProjectManagers = new SelectList(await _db.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullNameAr", model.ProjectManagerId);
            model.FinancialCosts = new SelectList(await _db.FinancialCosts.Where(f => f.IsActive).OrderBy(f => f.OrderIndex).Select(f => new { f.Id, f.NameAr }).ToListAsync(), "Id", "NameAr", model.FinancialCostId);
            if (model.ExternalUnitId.HasValue)
                model.SubObjectives = new SelectList(await _db.SubObjectives.Where(s => s.ExternalUnitId == model.ExternalUnitId.Value && s.IsActive).OrderBy(s => s.OrderIndex).Select(s => new { s.Id, s.NameAr }).ToListAsync(), "Id", "NameAr");
            model.SubObjectives ??= new SelectList(Enumerable.Empty<SelectListItem>());
        }

        public async Task PopulateFilterDropdownsAsync(ProjectListViewModel model, UserRole userRole, int userId)
        {
            var initiativesQuery = _db.Initiatives.Where(i => !i.IsDeleted);
            if (userRole == UserRole.Supervisor)
            {
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId || accessibleIds.Contains(i.Id));
            }
            else if (userRole != UserRole.Admin && userRole != UserRole.Executive)
            {
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                var managedProjectInitIds = await _db.Projects
                    .Where(p => !p.IsDeleted && p.ProjectManagerId == userId).Select(p => p.InitiativeId).Distinct().ToListAsync();
                var allIds = accessibleIds.Union(managedProjectInitIds).ToList();
                initiativesQuery = initiativesQuery.Where(i => allIds.Contains(i.Id));
            }
            model.Initiatives = new SelectList(await initiativesQuery.OrderBy(i => i.NameAr).ToListAsync(), "Id", "NameAr", model.InitiativeId);
        }

        #endregion

        #region Private Helpers

        private async Task<int?> ResolveProjectManagerIdAsync(string? empNumber)
        {
            if (string.IsNullOrWhiteSpace(empNumber)) return null;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.ADUsername == empNumber && u.IsActive);
            return user?.Id;
        }

        private decimal CalculateProjectProgress(Project project)
        {
            if (project.Steps == null || !project.Steps.Any()) return 0;
            var activeSteps = project.Steps.Where(s => !s.IsDeleted).ToList();
            if (!activeSteps.Any()) return 0;
            return activeSteps.Where(s => s.ProgressPercentage >= 100).Sum(s => s.Weight);
        }

        private async Task SaveRelatedDataAsync(int projectId, ProjectFormViewModel model)
        {
            // Requirements
            if (model.Requirements?.Any() == true)
            {
                var entities = model.Requirements.Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select((r, i) => new ProjectRequirement { ProjectId = projectId, RequirementText = r.Trim(), OrderIndex = i, CreatedAt = DateTime.Now }).ToList();
                if (entities.Any()) { _db.ProjectRequirements.AddRange(entities); await _db.SaveChangesAsync(); }
            }
            // KPIs
            if (model.KPIItems?.Any() == true)
            {
                var entities = model.KPIItems.Where(k => !string.IsNullOrWhiteSpace(k.KPIText))
                    .Select((k, i) => new ProjectKPI { ProjectId = projectId, KPIText = k.KPIText.Trim(), TargetValue = k.TargetValue?.Trim(), ActualValue = k.ActualValue?.Trim(), OrderIndex = i, CreatedAt = DateTime.Now }).ToList();
                if (entities.Any()) { _db.ProjectKPIs.AddRange(entities); await _db.SaveChangesAsync(); }
            }
            // Supporting Entities مع ممثلين متعددين
            if (model.SupportingEntitiesWithReps?.Any() == true)
            {
                foreach (var entityVm in model.SupportingEntitiesWithReps)
                {
                    var unit = new ProjectSupportingUnit
                    {
                        ProjectId = projectId,
                        ExternalUnitId = entityVm.ExternalUnitId,
                        ExternalUnitName = entityVm.UnitName,
                        CreatedAt = DateTime.Now
                    };

                    _db.ProjectSupportingUnits.Add(unit);
                    await _db.SaveChangesAsync(); // حفظ عشان ناخذ الـ Id

                    // حفظ الممثلين المتعددين
                    if (entityVm.Representatives?.Any() == true)
                    {
                        var reps = entityVm.Representatives
                            .Where(r => !string.IsNullOrWhiteSpace(r.EmpNumber))
                            .Select((r, i) => new SupportingUnitRepresentative
                            {
                                ProjectSupportingUnitId = unit.Id,
                                EmpNumber = r.EmpNumber,
                                Name = r.Name,
                                Rank = r.Rank,
                                OrderIndex = i,
                                CreatedAt = DateTime.Now
                            }).ToList();

                        if (reps.Any())
                        {
                            _db.SupportingUnitRepresentatives.AddRange(reps);
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }
            // Year Targets
            if (model.YearTargets?.Any() == true)
            {
                var entities = model.YearTargets.Where(y => y.TargetPercentage > 0)
                    .Select(y => new ProjectYearTarget { ProjectId = projectId, Year = y.Year, TargetPercentage = y.TargetPercentage, Notes = y.Notes?.Trim(), CreatedAt = DateTime.Now }).ToList();
                if (entities.Any()) { _db.ProjectYearTargets.AddRange(entities); await _db.SaveChangesAsync(); }
            }
            // SubObjectives (many-to-many)
            if (model.SubObjectiveIds?.Any() == true)
            {
                var entities = model.SubObjectiveIds.Distinct()
                    .Select(soId => new ProjectSubObjective { ProjectId = projectId, SubObjectiveId = soId }).ToList();
                _db.ProjectSubObjectives.AddRange(entities);
                await _db.SaveChangesAsync();
            }
        }

        private async Task<List<Guid>> GetUnitAndChildrenIdsAsync(Guid unitId)
        {
            var result = new List<Guid> { unitId };
            var children = await _db.ExternalOrganizationalUnits.Where(u => u.ParentId == unitId && u.IsActive).Select(u => u.Id).ToListAsync();
            foreach (var childId in children)
            {
                result.Add(childId);
                var grandChildren = await _db.ExternalOrganizationalUnits.Where(u => u.ParentId == childId && u.IsActive).Select(u => u.Id).ToListAsync();
                result.AddRange(grandChildren);
            }
            return result;
        }

        #endregion
    }
}
