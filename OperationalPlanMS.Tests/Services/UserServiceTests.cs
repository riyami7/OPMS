using FluentAssertions;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Services;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class UserServiceTests : IDisposable
    {
        private readonly Data.AppDbContext _db;
        private readonly UserService _service;

        public UserServiceTests()
        {
            _db = TestDbHelper.CreateContext();
            _service = new UserService(_db, TestDbHelper.CreateLogger<UserService>());
            TestDbHelper.SeedBasicDataAsync(_db).GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task GetUsersAsync_NoFilters_ReturnsAllUsers()
        {
            var result = await _service.GetUsersAsync(null, null, null, 1);
            result.Users.Should().HaveCount(5);
            result.TotalCount.Should().Be(5);
        }

        [Fact]
        public async Task GetUsersAsync_SearchByName_ReturnsMatching()
        {
            var result = await _service.GetUsersAsync("أحمد", null, null, 1);
            result.Users.Should().HaveCount(1);
            result.Users.First().FullNameAr.Should().Contain("أحمد");
        }

        [Fact]
        public async Task GetUsersAsync_FilterByRole_ReturnsCorrectRole()
        {
            var result = await _service.GetUsersAsync(null, 3, null, 1);
            result.Users.Should().OnlyContain(u => u.RoleId == 3);
        }

        [Fact]
        public async Task GetUsersAsync_FilterByActive_ReturnsOnlyActive()
        {
            var result = await _service.GetUsersAsync(null, null, true, 1);
            result.Users.Should().OnlyContain(u => u.IsActive);
            result.TotalCount.Should().Be(4);
        }

        [Fact]
        public async Task GetUsersAsync_FilterByInactive_ReturnsOnlyInactive()
        {
            var result = await _service.GetUsersAsync(null, null, false, 1);
            result.Users.Should().OnlyContain(u => !u.IsActive);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetUsersAsync_Pagination_RespectsPageSize()
        {
            var result = await _service.GetUsersAsync(null, null, null, 1, pageSize: 2);
            result.Users.Should().HaveCount(2);
            result.TotalCount.Should().Be(5);
        }

        [Fact]
        public async Task GetUsersAsync_Page2_ReturnsRemainingUsers()
        {
            var result = await _service.GetUsersAsync(null, null, null, 2, pageSize: 3);
            result.Users.Should().HaveCount(2);
            result.CurrentPage.Should().Be(2);
        }

        [Fact]
        public async Task GetByIdAsync_ExistingUser_ReturnsUser()
        {
            var user = await _service.GetByIdAsync(1);
            user.Should().NotBeNull();
            user!.ADUsername.Should().Be("C1-0001");
        }

        [Fact]
        public async Task GetByIdAsync_NonExisting_ReturnsNull()
        {
            var user = await _service.GetByIdAsync(999);
            user.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_ValidUser_SucceedsAndSavesToDb()
        {
            var model = new UserFormViewModel
            {
                ADUsername = "C1-9999", FullNameAr = "مستخدم جديد",
                FullNameEn = "New User", RoleId = 4, IsActive = true
            };
            var (success, error) = await _service.CreateAsync(model, createdBy: 3);
            success.Should().BeTrue();
            error.Should().BeNull();
            var saved = _db.Users.FirstOrDefault(u => u.ADUsername == "C1-9999");
            saved.Should().NotBeNull();
            saved!.FullNameAr.Should().Be("مستخدم جديد");
        }

        [Fact]
        public async Task CreateAsync_DuplicateUsername_Fails()
        {
            var model = new UserFormViewModel
            {
                ADUsername = "C1-0001", FullNameAr = "مكرر",
                FullNameEn = "Duplicate", RoleId = 4
            };
            var (success, error) = await _service.CreateAsync(model, createdBy: 3);
            success.Should().BeFalse();
            error.Should().Contain("موجود بالفعل");
        }

        [Fact]
        public async Task UpdateAsync_ValidUpdate_Succeeds()
        {
            var model = new UserFormViewModel
            {
                ADUsername = "C1-0001", FullNameAr = "أحمد المحدث",
                FullNameEn = "Ahmed Updated", RoleId = 3, IsActive = true
            };
            var (success, error) = await _service.UpdateAsync(1, model);
            success.Should().BeTrue();
            var user = await _db.Users.FindAsync(1);
            user!.FullNameAr.Should().Be("أحمد المحدث");
        }

        [Fact]
        public async Task UpdateAsync_NonExistingUser_Fails()
        {
            var model = new UserFormViewModel { ADUsername = "C1-XXXX", FullNameAr = "غير موجود", RoleId = 4 };
            var (success, error) = await _service.UpdateAsync(999, model);
            success.Should().BeFalse();
            error.Should().Contain("غير موجود");
        }

        [Fact]
        public async Task UpdateAsync_DuplicateUsername_Fails()
        {
            var model = new UserFormViewModel { ADUsername = "C1-0002", FullNameAr = "تعارض", RoleId = 4 };
            var (success, error) = await _service.UpdateAsync(1, model);
            success.Should().BeFalse();
            error.Should().Contain("موجود بالفعل");
        }

        [Fact]
        public async Task DeleteAsync_UserWithNoRelations_Succeeds()
        {
            var (success, error) = await _service.DeleteAsync(5, currentUserId: 3);
            success.Should().BeTrue();
            _db.Users.Find(5).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_Self_Fails()
        {
            var (success, error) = await _service.DeleteAsync(3, currentUserId: 3);
            success.Should().BeFalse();
            error.Should().Contain("حسابك الخاص");
        }

        [Fact]
        public async Task DeleteAsync_NonExisting_Fails()
        {
            var (success, error) = await _service.DeleteAsync(999, currentUserId: 3);
            success.Should().BeFalse();
            error.Should().Contain("غير موجود");
        }

        [Fact]
        public async Task DeleteAsync_UserWithInitiatives_Fails()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1);
            var (success, error) = await _service.DeleteAsync(1, currentUserId: 3);
            success.Should().BeFalse();
            error.Should().Contain("مبادرات");
        }

        [Fact]
        public async Task DeleteAsync_UserWithProjects_Fails()
        {
            var initiative = await TestDbHelper.SeedInitiativeAsync(_db);
            await TestDbHelper.SeedProjectAsync(_db, initiative.Id, managerId: 2);
            var (success, error) = await _service.DeleteAsync(2, currentUserId: 3);
            success.Should().BeFalse();
            error.Should().Contain("مشاريع");
        }

        [Fact]
        public async Task DeleteAsync_UserWithSteps_Fails()
        {
            var initiative = await TestDbHelper.SeedInitiativeAsync(_db);
            var project = await TestDbHelper.SeedProjectAsync(_db, initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, assignedToId: 4);
            var (success, error) = await _service.DeleteAsync(4, currentUserId: 3);
            success.Should().BeFalse();
            error.Should().Contain("خطوات");
        }

        [Fact]
        public async Task ToggleActiveAsync_ActiveUser_Deactivates()
        {
            var (success, message) = await _service.ToggleActiveAsync(1);
            success.Should().BeTrue();
            message.Should().Contain("تعطيل");
            var user = await _db.Users.FindAsync(1);
            user!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task ToggleActiveAsync_InactiveUser_Activates()
        {
            var (success, message) = await _service.ToggleActiveAsync(5);
            success.Should().BeTrue();
            message.Should().Contain("تفعيل");
        }

        [Fact]
        public async Task ToggleActiveAsync_NonExisting_Fails()
        {
            var (success, message) = await _service.ToggleActiveAsync(999);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task IsUsernameTakenAsync_Existing_ReturnsTrue()
        {
            var taken = await _service.IsUsernameTakenAsync("C1-0001");
            taken.Should().BeTrue();
        }

        [Fact]
        public async Task IsUsernameTakenAsync_NonExisting_ReturnsFalse()
        {
            var taken = await _service.IsUsernameTakenAsync("C1-XXXX");
            taken.Should().BeFalse();
        }

        [Fact]
        public async Task IsUsernameTakenAsync_ExcludeSelf_ReturnsFalse()
        {
            var taken = await _service.IsUsernameTakenAsync("C1-0001", excludeId: 1);
            taken.Should().BeFalse();
        }

      [Fact]
        public async Task SyncUserAssignmentsAsync_DoesNotThrow()
        {
            // ExecuteUpdateAsync not supported by InMemory - just verify no exception
            var act = () => _service.SyncUserAssignmentsAsync(userId: 2, empNumber: "C1-0002");
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GetFormViewModelAsync_ExistingUser_ReturnsPopulatedViewModel()
        {
            var result = await _service.GetFormViewModelAsync(1);
            result.Should().NotBeNull();
            result.ADUsername.Should().Be("C1-0001");
            result.Roles.Should().NotBeNull();
        }

        [Fact]
        public async Task GetFormViewModelAsync_NonExisting_ThrowsKeyNotFound()
        {
            var act = () => _service.GetFormViewModelAsync(999);
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_ReturnsWithDropdowns()
        {
            var result = await _service.PrepareCreateViewModelAsync();
            result.Should().NotBeNull();
            result.Roles.Should().NotBeNull();
        }
    }
}
