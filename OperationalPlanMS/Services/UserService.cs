using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Services
{
    /// <summary>
    /// واجهة خدمة إدارة المستخدمين
    /// </summary>
    public interface IUserService
    {
        Task<UserListViewModel> GetUsersAsync(string? searchTerm, int? roleId, bool? isActive, int page, int pageSize = 20);
        Task<User?> GetByIdAsync(int id);
        Task<UserFormViewModel> GetFormViewModelAsync(int id);
        Task<UserFormViewModel> PrepareCreateViewModelAsync();
        Task<(bool Success, string? Error)> CreateAsync(UserFormViewModel model, int createdBy);
        Task<(bool Success, string? Error)> UpdateAsync(int id, UserFormViewModel model);
        Task<(bool Success, string? Error)> DeleteAsync(int id, int currentUserId);
        Task<(bool Success, string Message)> ToggleActiveAsync(int id);
        Task<bool> IsUsernameTakenAsync(string username, int? excludeId = null);
        Task PopulateDropdownsAsync(UserFormViewModel model);
        Task SyncUserAssignmentsAsync(int userId, string empNumber);
    }

    /// <summary>
    /// خدمة إدارة المستخدمين — Business Logic
    /// </summary>
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<UserListViewModel> GetUsersAsync(string? searchTerm, int? roleId, bool? isActive, int page, int pageSize = 20)
        {
            var query = _db.Users.Include(u => u.Role).AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(u => u.FullNameAr.Contains(searchTerm) ||
                                        u.FullNameEn.Contains(searchTerm) ||
                                        u.ADUsername.Contains(searchTerm) ||
                                        u.Email!.Contains(searchTerm));

            if (roleId.HasValue)
                query = query.Where(u => u.RoleId == roleId.Value);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            return new UserListViewModel
            {
                Users = await query.OrderBy(u => u.FullNameAr)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
                SearchTerm = searchTerm,
                RoleId = roleId,
                IsActive = isActive,
                Roles = new SelectList(await _db.Roles.ToListAsync(), "Id", "NameAr"),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize
            };
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _db.Users.FindAsync(id);
        }

        public async Task<UserFormViewModel> GetFormViewModelAsync(int id)
        {
            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                throw new KeyNotFoundException($"المستخدم غير موجود: {id}");

            var viewModel = UserFormViewModel.FromEntity(entity);
            await PopulateDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<UserFormViewModel> PrepareCreateViewModelAsync()
        {
            var viewModel = new UserFormViewModel();
            await PopulateDropdownsAsync(viewModel);
            return viewModel;
        }

        public async Task<(bool Success, string? Error)> CreateAsync(UserFormViewModel model, int createdBy)
        {
            if (await IsUsernameTakenAsync(model.ADUsername))
                return (false, "رقم الموظف موجود بالفعل");

            var entity = new User
            {
                CreatedAt = DateTime.Now,
                CreatedBy = createdBy
            };
            model.UpdateEntity(entity);

            _db.Users.Add(entity);
            await _db.SaveChangesAsync();

            // ربط المشاريع والخطوات التي عُيِّن عليها هذا الموظف مسبقاً
            await SyncUserAssignmentsAsync(entity.Id, entity.ADUsername);

            _logger.LogInformation("تم إنشاء مستخدم جديد: {Username} بواسطة UserId: {CreatedBy}", entity.ADUsername, createdBy);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, UserFormViewModel model)
        {
            if (await IsUsernameTakenAsync(model.ADUsername, id))
                return (false, "رقم الموظف موجود بالفعل");

            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return (false, "المستخدم غير موجود");

            model.UpdateEntity(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("تم تحديث المستخدم: {Username}", entity.ADUsername);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(int id, int currentUserId)
        {
            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return (false, "المستخدم غير موجود");

            if (entity.Id == currentUserId)
                return (false, "لا يمكنك حذف حسابك الخاص");

            // التحقق من الارتباطات
            if (await _db.Initiatives.AnyAsync(i => i.SupervisorId == id || i.CreatedById == id))
                return (false, "لا يمكن حذف المستخدم لوجود مبادرات مرتبطة به");

            if (await _db.Projects.AnyAsync(p => p.ProjectManagerId == id))
                return (false, "لا يمكن حذف المستخدم لوجود مشاريع مرتبطة به");

            if (await _db.Steps.AnyAsync(s => s.AssignedToId == id))
                return (false, "لا يمكن حذف المستخدم لوجود خطوات مُسندة إليه");

            _db.Users.Remove(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("تم حذف المستخدم: {Username} بواسطة UserId: {DeletedBy}", entity.ADUsername, currentUserId);

            return (true, null);
        }

        public async Task<(bool Success, string Message)> ToggleActiveAsync(int id)
        {
            var entity = await _db.Users.FindAsync(id);
            if (entity == null)
                return (false, "المستخدم غير موجود");

            entity.IsActive = !entity.IsActive;
            await _db.SaveChangesAsync();

            var message = entity.IsActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم";
            _logger.LogInformation("{Action} المستخدم: {Username}", message, entity.ADUsername);

            return (true, message);
        }

        public async Task<bool> IsUsernameTakenAsync(string username, int? excludeId = null)
        {
            var query = _db.Users.Where(u => u.ADUsername == username);
            if (excludeId.HasValue)
                query = query.Where(u => u.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task PopulateDropdownsAsync(UserFormViewModel model)
        {
            model.Roles = new SelectList(await _db.Roles.ToListAsync(), "Id", "NameAr", model.RoleId);
        }

        public async Task SyncUserAssignmentsAsync(int userId, string empNumber)
        {
            try
            {
                await _db.Projects
                    .Where(p => p.ProjectManagerEmpNumber == empNumber
                             && p.ProjectManagerId == null
                             && !p.IsDeleted)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProjectManagerId, userId));

                await _db.Steps
                    .Where(s => s.AssignedToEmpNumber == empNumber
                             && s.AssignedToId == null
                             && !s.IsDeleted)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.AssignedToId, userId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل ربط المهام للمستخدم: {EmpNumber}", empNumber);
            }
        }
    }
}
