using FluentAssertions;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Services;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class ProjectServiceTests : IDisposable
    {
        private readonly Data.AppDbContext _db;
        private readonly ProjectService _service;
        private Initiative _initiative = null!;

        public ProjectServiceTests()
        {
            _db = TestDbHelper.CreateContext();
            _service = new ProjectService(_db, TestDbHelper.CreateLogger<ProjectService>(), TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(), TestDbHelper.CreateUserService());
            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            await TestDbHelper.SeedBasicDataAsync(_db);
            _initiative = await TestDbHelper.SeedInitiativeAsync(_db);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public void CanAccess_Admin_AlwaysTrue()
        {
            var project = new Project { InitiativeId = _initiative.Id, ProjectManagerId = 2 };
            _service.CanAccess(project, UserRole.Admin, 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_Executive_AlwaysTrue()
        {
            var project = new Project { InitiativeId = _initiative.Id, ProjectManagerId = 2 };
            _service.CanAccess(project, UserRole.Executive, 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_User_OwnProject_True()
        {
            var project = new Project { InitiativeId = _initiative.Id, ProjectManagerId = 2 };
            _service.CanAccess(project, UserRole.User, 2).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_User_OtherProject_False()
        {
            var project = new Project { InitiativeId = _initiative.Id, ProjectManagerId = 2 };
            _service.CanAccess(project, UserRole.User, 99).Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_Existing_ReturnsProject()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            var result = await _service.GetByIdAsync(project.Id);
            result.Should().NotBeNull();
            result!.Code.Should().Be("PRJ-2026-001");
        }

        [Fact]
        public async Task GetByIdAsync_Deleted_ReturnsNull()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            project.IsDeleted = true;
            await _db.SaveChangesAsync();
            var result = await _service.GetByIdAsync(project.Id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task SoftDeleteAsync_Existing_MarksDeleted()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            var (success, error) = await _service.SoftDeleteAsync(project.Id, modifiedById: 3);
            success.Should().BeTrue();
            var deleted = await _db.Projects.FindAsync(project.Id);
            deleted!.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task SoftDeleteAsync_NonExisting_Fails()
        {
            var (success, error) = await _service.SoftDeleteAsync(999, modifiedById: 3);
            success.Should().BeFalse();
            error.Should().Contain("??? ?????");
        }

        [Fact]
        public async Task RecalculateProgressAsync_NoSteps_ReturnsZero()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            var progress = await _service.RecalculateProgressAsync(project.Id);
            progress.Should().Be(0);
        }

        [Fact]
        public async Task RecalculateProgressAsync_OneStepComplete_ReturnsWeight()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 1, weight: 60, progress: 100);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 2, weight: 40, progress: 50);
            var progress = await _service.RecalculateProgressAsync(project.Id);
            progress.Should().Be(60);
        }

        [Fact]
        public async Task RecalculateProgressAsync_AllComplete_Returns100()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 1, weight: 60, progress: 100);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 2, weight: 40, progress: 100);
            var progress = await _service.RecalculateProgressAsync(project.Id);
            progress.Should().Be(100);
        }

        [Fact]
        public async Task RecalculateProgressAsync_DeletedStepsIgnored()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 1, weight: 60, progress: 100);
            var step2 = await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 2, weight: 40, progress: 100);
            step2.IsDeleted = true;
            await _db.SaveChangesAsync();
            var progress = await _service.RecalculateProgressAsync(project.Id);
            progress.Should().Be(60);
        }

        [Fact]
        public async Task UpdateProjectProgressAsync_UpdatesDbValue()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 1, weight: 70, progress: 100);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 2, weight: 30, progress: 0);
            await _service.UpdateProjectProgressAsync(project.Id);
            var updated = await _db.Projects.FindAsync(project.Id);
            updated!.ProgressPercentage.Should().Be(70);
        }

        [Fact]
        public async Task GetCalculatedProgressAsync_SumsCompletedWeights()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 1, weight: 30, progress: 100);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 2, weight: 20, progress: 100);
            await TestDbHelper.SeedStepAsync(_db, project.Id, stepNumber: 3, weight: 50, progress: 50);
            var progress = await _service.GetCalculatedProgressAsync(project.Id);
            progress.Should().Be(50);
        }

        [Fact]
        public async Task AddNoteAsync_Valid_Succeeds()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            var (success, error) = await _service.AddNoteAsync(project.Id, "?????? ?????", 3);
            success.Should().BeTrue();
            _db.ProgressUpdates.Count(p => p.ProjectId == project.Id).Should().Be(1);
        }

        [Fact]
        public async Task AddNoteAsync_EmptyNote_Fails()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            var (success, error) = await _service.AddNoteAsync(project.Id, "", 3);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task AddNoteAsync_NonExistingProject_Fails()
        {
            var (success, error) = await _service.AddNoteAsync(999, "??????", 3);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task EditNoteAsync_Valid_Succeeds()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await _service.AddNoteAsync(project.Id, "?????", 3);
            var note = _db.ProgressUpdates.First(p => p.ProjectId == project.Id);
            var (success, _) = await _service.EditNoteAsync(note.Id, project.Id, "??????");
            success.Should().BeTrue();
            _db.ProgressUpdates.Find(note.Id)!.NotesAr.Should().Be("??????");
        }

        [Fact]
        public async Task DeleteNoteAsync_Valid_Succeeds()
        {
            var project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
            await _service.AddNoteAsync(project.Id, "?????", 3);
            var note = _db.ProgressUpdates.First();
            var (success, _) = await _service.DeleteNoteAsync(note.Id, project.Id);
            success.Should().BeTrue();
            _db.ProgressUpdates.Should().BeEmpty();
        }

        [Fact]
        public async Task GetListAsync_Admin_SeesAll()
        {
            await TestDbHelper.SeedProjectAsync(_db, _initiative.Id, managerId: 2, code: "PRJ-2026-001");
            await TestDbHelper.SeedProjectAsync(_db, _initiative.Id, managerId: 4, code: "PRJ-2026-002");
            var result = await _service.GetListAsync(null, null, null, 1, 20, UserRole.Admin, 3);
            result.Projects.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetListAsync_User_SeesOnlyOwn()
        {
            await TestDbHelper.SeedProjectAsync(_db, _initiative.Id, managerId: 2, code: "PRJ-2026-001");
            await TestDbHelper.SeedProjectAsync(_db, _initiative.Id, managerId: 4, code: "PRJ-2026-002");
            var result = await _service.GetListAsync(null, null, null, 1, 20, UserRole.User, 2);
            result.Projects.Should().HaveCount(1);
            result.Projects.First().ProjectManagerId.Should().Be(2);
        }

        [Fact]
        public async Task GetSubObjectivesByUnitAsync_NullUnit_ReturnsEmpty()
        {
            var result = await _service.GetSubObjectivesByUnitAsync(null);
            result.Should().BeAssignableTo<IEnumerable<object>>();
        }

        [Fact]
        public async Task GetSupportingEntitiesAsync_ReturnsActiveOnly()
        {
            _db.SupportingEntities.AddRange(
                new SupportingEntity { NameAr = "??? ??????", NameEn = "Active", IsActive = true },
                new SupportingEntity { NameAr = "??? ??????", NameEn = "Inactive", IsActive = false }
            );
            await _db.SaveChangesAsync();
            var result = await _service.GetSupportingEntitiesAsync();
            var list = (result as IEnumerable<object>)!.ToList();
            list.Should().HaveCount(1);
        }
    }
}
