using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Services
{
    public interface IInitiativeService
    {
        // القراءة
        Task<InitiativeListViewModel> GetListAsync(string? searchTerm, int? fiscalYearId, Guid? externalUnitId, int page, int pageSize, UserRole userRole, int userId);
        Task<InitiativeDetailsViewModel?> GetDetailsAsync(int id);
        Task<Initiative?> GetByIdAsync(int id);

        // الإنشاء والتعديل
        Task<InitiativeFormViewModel> PrepareCreateViewModelAsync();
        Task<InitiativeFormViewModel?> PrepareEditViewModelAsync(int id);
        Task<(bool Success, int? Id, string? Error)> CreateAsync(InitiativeFormViewModel model, int createdById);
        Task<(bool Success, string? Error)> UpdateAsync(int id, InitiativeFormViewModel model, int modifiedById, UserRole userRole, int userId);
        Task<(bool Success, string? Error)> SoftDeleteAsync(int id, int modifiedById, UserRole userRole, int userId);

        // الملاحظات
        Task<(bool Success, string? Error)> AddNoteAsync(int initiativeId, string notes, int createdById);
        Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int initiativeId, string notes);
        Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int initiativeId);

        // المساعدة
        Task PopulateFormDropdownsAsync(InitiativeFormViewModel model);
        Task PopulateFilterDropdownsAsync(InitiativeListViewModel model);
        bool CanAccess(Initiative initiative, UserRole userRole, int userId);
        AccessLevel? GetAccessLevel(int initiativeId, UserRole userRole, int userId);

        // معلومات الوحدة التنظيمية
        Task<string?> GetUnitNameAsync(Guid externalUnitId);
    }

    public class InitiativeService : IInitiativeService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<InitiativeService> _logger;
        private readonly IAuditService _audit;
        private readonly IUserService _userService;

        public InitiativeService(AppDbContext db, ILogger<InitiativeService> logger, IAuditService audit, IUserService userService)
        {
            _db = db;
            _logger = logger;
            _audit = audit;
            _userService = userService;
        }

        #region القراءة

        public async Task<InitiativeListViewModel> GetListAsync(
            string? searchTerm, int? fiscalYearId, Guid? externalUnitId,
            int page, int pageSize, UserRole userRole, int userId)
        {
            var query = _db.Initiatives.Where(i => !i.IsDeleted)
                .Include(i => i.FiscalYear).Include(i => i.Supervisor)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .AsQueryable();

            // تصفية حسب الدور
            if (userRole == UserRole.Supervisor)
            {
                // Supervisor sees own initiatives + managed projects + InitiativeAccess
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive)
                    .Select(a => a.InitiativeId).ToListAsync();
                var managedProjectInitiativeIds = await _db.Projects
                    .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                    .Select(p => p.InitiativeId).Distinct().ToListAsync();
                query = query.Where(i => i.SupervisorId == userId
                    || accessibleIds.Contains(i.Id)
                    || managedProjectInitiativeIds.Contains(i.Id));
            }
            else if (userRole != UserRole.Admin && userRole != UserRole.Executive)
            {
                // User, StepUser — sees initiatives where they are:
                // - project manager or deputy
                // - step assignee
                // - via InitiativeAccess
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive)
                    .Select(a => a.InitiativeId).ToListAsync();

                var managedProjectInitiativeIds = await _db.Projects
                    .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                    .Select(p => p.InitiativeId).Distinct().ToListAsync();

                var empNumber = await _db.Users.Where(u => u.Id == userId).Select(u => u.ADUsername).FirstOrDefaultAsync();
                var deputyInitiativeIds = !string.IsNullOrEmpty(empNumber)
                    ? await _db.Projects.Where(p => !p.IsDeleted && p.DeputyManagerEmpNumber == empNumber)
                        .Select(p => p.InitiativeId).Distinct().ToListAsync()
                    : new List<int>();

                var stepInitiativeIds = await _db.Steps
                    .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                    .Select(s => s.Project.InitiativeId).Distinct().ToListAsync();

                var allAccessibleIds = accessibleIds
                    .Union(managedProjectInitiativeIds)
                    .Union(deputyInitiativeIds)
                    .Union(stepInitiativeIds)
                    .Distinct().ToList();

                if (allAccessibleIds.Any())
                    query = query.Where(i => allAccessibleIds.Contains(i.Id));
                else
                    query = query.Where(i => false);
            }

            // البحث
            if (!string.IsNullOrWhiteSpace(searchTerm))
                query = query.Where(i => i.NameAr.Contains(searchTerm) ||
                    i.NameEn.Contains(searchTerm) || i.Code.Contains(searchTerm));

            // السنة المالية
            if (fiscalYearId.HasValue)
                query = query.Where(i => i.FiscalYearId == fiscalYearId.Value);

            // الوحدة التنظيمية (مع الوحدات الفرعية)
            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIdsAsync(externalUnitId.Value);
                query = query.Where(i => i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var totalCount = await query.CountAsync();
            var initiatives = await query.OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new InitiativeListViewModel
            {
                Initiatives = initiatives,
                SearchTerm = searchTerm,
                FiscalYearId = fiscalYearId,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };

            await PopulateFilterDropdownsAsync(model);
            return model;
        }

        public async Task<InitiativeDetailsViewModel?> GetDetailsAsync(int id)
        {
            var initiative = await _db.Initiatives
                .Include(i => i.FiscalYear).Include(i => i.Supervisor).Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null) return null;

            return new InitiativeDetailsViewModel
            {
                Initiative = initiative,
                Projects = await _db.Projects.Where(p => p.InitiativeId == id && !p.IsDeleted)
                    .Include(p => p.ProjectManager).Include(p => p.Steps.Where(s => !s.IsDeleted)).ToListAsync(),
                Notes = await _db.ProgressUpdates.Where(p => p.InitiativeId == id)
                    .Include(p => p.CreatedBy).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync()
            };
        }

        public async Task<Initiative?> GetByIdAsync(int id)
        {
            return await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        }

        #endregion

        #region الإنشاء والتعديل

        public async Task<InitiativeFormViewModel> PrepareCreateViewModelAsync()
        {
            var viewModel = new InitiativeFormViewModel();

            // توليد الكود التلقائي
            var currentYear = DateTime.Now.Year;
            var lastCode = await _db.Initiatives
                .Where(i => i.Code.StartsWith($"INI-{currentYear}"))
                .OrderByDescending(i => i.Code).Select(i => i.Code).FirstOrDefaultAsync();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var parts = lastCode.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int last)) nextNumber = last + 1;
            }
            viewModel.Code = $"INI-{currentYear}-{nextNumber:D3}";

            await PopulateFormDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<InitiativeFormViewModel?> PrepareEditViewModelAsync(int id)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return null;

            var viewModel = InitiativeFormViewModel.FromEntity(initiative);
            await PopulateFormDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<(bool Success, int? Id, string? Error)> CreateAsync(InitiativeFormViewModel model, int createdById)
        {
            if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code))
                return (false, null, "هذا الكود مستخدم بالفعل");

            var initiative = new Initiative
            {
                CreatedById = createdById,
                CreatedAt = DateTime.Now
            };
            model.UpdateEntity(initiative);

            // ربط المشرف — ينشئه تلقائياً إذا ما موجود
            initiative.SupervisorId = await _userService.EnsureUserExistsAsync(
                model.SupervisorEmpNumber, model.SupervisorName, model.SupervisorRank, "Supervisor");

            _db.Initiatives.Add(initiative);
            await _db.SaveChangesAsync();

            _logger.LogInformation("تم إنشاء مبادرة: {Code} بواسطة UserId: {UserId}", initiative.Code, createdById);
            await _audit.LogAsync("Initiative", initiative.Id, initiative.NameAr, "Create", createdById, $"كود: {initiative.Code}");

            return (true, initiative.Id, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, InitiativeFormViewModel model, int modifiedById, UserRole userRole, int userId)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return (false, "المبادرة غير موجودة");

            // Supervisor يعدل مبادراته فقط
            if (userRole == UserRole.Supervisor && initiative.SupervisorId != userId)
                return (false, "لا يمكنك تعديل هذه المبادرة");

            if (await _db.Initiatives.AnyAsync(i => i.Code == model.Code && i.Id != id))
                return (false, "هذا الكود مستخدم بالفعل");

            model.UpdateEntity(initiative);

            // ربط المشرف — ينشئه تلقائياً إذا ما موجود
            initiative.SupervisorId = await _userService.EnsureUserExistsAsync(
                model.SupervisorEmpNumber, model.SupervisorName, model.SupervisorRank, "Supervisor");

            initiative.LastModifiedById = modifiedById;
            initiative.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("تم تحديث مبادرة: {Code}", initiative.Code);
            await _audit.LogAsync("Initiative", initiative.Id, initiative.NameAr, "Update", modifiedById, $"كود: {initiative.Code}");

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id, int modifiedById, UserRole userRole, int userId)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (initiative == null) return (false, "المبادرة غير موجودة");

            // Supervisor يحذف مبادراته فقط
            if (userRole == UserRole.Supervisor && initiative.SupervisorId != userId)
                return (false, "لا يمكنك حذف هذه المبادرة");

            initiative.IsDeleted = true;
            initiative.LastModifiedById = modifiedById;
            initiative.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("تم حذف مبادرة: {Code} بواسطة UserId: {UserId}", initiative.Code, modifiedById);
            await _audit.LogAsync("Initiative", initiative.Id, initiative.NameAr, "Delete", modifiedById, $"كود: {initiative.Code}");

            return (true, null);
        }

        #endregion

        #region الملاحظات

        public async Task<(bool Success, string? Error)> AddNoteAsync(int initiativeId, string notes, int createdById)
        {
            var initiative = await _db.Initiatives.FirstOrDefaultAsync(i => i.Id == initiativeId && !i.IsDeleted);
            if (initiative == null) return (false, "المبادرة غير موجودة");
            if (string.IsNullOrWhiteSpace(notes)) return (false, "الملاحظة مطلوبة");

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                InitiativeId = initiativeId,
                NotesAr = notes,
                UpdateType = UpdateType.Note,
                CreatedById = createdById,
                CreatedAt = DateTime.Now
            });

            initiative.LastModifiedById = createdById;
            initiative.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int initiativeId, string notes)
        {
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId) return (false, "الملاحظة غير موجودة");
            if (string.IsNullOrWhiteSpace(notes)) return (false, "الملاحظة مطلوبة");

            note.NotesAr = notes;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int initiativeId)
        {
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.InitiativeId != initiativeId) return (false, "الملاحظة غير موجودة");

            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        #endregion

        #region المساعدة

        public bool CanAccess(Initiative initiative, UserRole userRole, int userId)
        {
            // Admin and Executive see everything
            if (userRole == UserRole.Admin || userRole == UserRole.Executive)
                return true;

            // Supervisor sees own initiatives
            if (userRole == UserRole.Supervisor && initiative.SupervisorId == userId)
                return true;

            // For ALL roles: check InitiativeAccess table as fallback
            return _db.InitiativeAccess.Any(a =>
                a.InitiativeId == initiative.Id &&
                a.UserId == userId &&
                a.IsActive);
        }

        /// <summary>
        /// Get the access level for a user on a specific initiative.
        /// Returns null if no access.
        /// </summary>
        public AccessLevel? GetAccessLevel(int initiativeId, UserRole userRole, int userId)
        {
            if (userRole == UserRole.Admin) return AccessLevel.FullAccess;
            if (userRole == UserRole.Executive) return AccessLevel.ReadOnly;

            // Supervisor of this initiative gets FullAccess
            if (userRole == UserRole.Supervisor)
            {
                var isSupervisor = _db.Initiatives.Any(i => i.Id == initiativeId && i.SupervisorId == userId && !i.IsDeleted);
                if (isSupervisor) return AccessLevel.FullAccess;
            }

            // For ALL roles (including Supervisor): check InitiativeAccess table
            var accessRecord = _db.InitiativeAccess
                .FirstOrDefault(a => a.InitiativeId == initiativeId && a.UserId == userId && a.IsActive);

            return accessRecord?.AccessLevel;
        }

        public async Task PopulateFormDropdownsAsync(InitiativeFormViewModel model)
        {
            model.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(), "Id", "NameAr", model.FiscalYearId);
            model.Supervisors = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullNameAr", model.SupervisorId);
        }

        public async Task PopulateFilterDropdownsAsync(InitiativeListViewModel model)
        {
            model.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(), "Id", "NameAr", model.FiscalYearId);
        }

        public async Task<string?> GetUnitNameAsync(Guid externalUnitId)
        {
            var unit = await _db.ExternalOrganizationalUnits.FirstOrDefaultAsync(u => u.Id == externalUnitId);
            return unit?.ArabicName ?? unit?.ArabicUnitName;
        }

        private async Task<List<Guid>> GetUnitAndChildrenIdsAsync(Guid unitId)
        {
            var result = new List<Guid> { unitId };
            var children = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == unitId && u.IsActive).Select(u => u.Id).ToListAsync();

            foreach (var childId in children)
            {
                result.Add(childId);
                var grandChildren = await _db.ExternalOrganizationalUnits
                    .Where(u => u.ParentId == childId && u.IsActive).Select(u => u.Id).ToListAsync();
                result.AddRange(grandChildren);
            }
            return result;
        }

        #endregion
    }
}
