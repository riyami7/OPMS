using FluentAssertions;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Services;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class StepServiceTests : IDisposable
    {
        private readonly Data.AppDbContext _db;
        private readonly StepService _service;
        private Initiative _initiative = null!;
        private Project _project = null!;

        public StepServiceTests()
        {
            _db = TestDbHelper.CreateContext();
            _service = new StepService(_db, TestDbHelper.CreateLogger<StepService>(), TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(), TestDbHelper.CreateUserService());
            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            await TestDbHelper.SeedBasicDataAsync(_db);
            _initiative = await TestDbHelper.SeedInitiativeAsync(_db);
            _project = await TestDbHelper.SeedProjectAsync(_db, _initiative.Id);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public void CanAccessStep_Admin_AlwaysTrue()
        {
            var step = new Step { ProjectId = _project.Id, AssignedToId = 4 };
            _service.CanAccessStep(step, UserRole.Admin, 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccessStep_Executive_AlwaysTrue()
        {
            var step = new Step { ProjectId = _project.Id, AssignedToId = 4 };
            _service.CanAccessStep(step, UserRole.Executive, 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccessStep_StepUser_OwnStep_True()
        {
            var step = new Step { ProjectId = _project.Id, AssignedToId = 4 };
            _service.CanAccessStep(step, UserRole.StepUser, 4).Should().BeTrue();
        }

        [Fact]
        public void CanAccessStep_StepUser_OtherStep_False()
        {
            var step = new Step { ProjectId = _project.Id, AssignedToId = 4 };
            _service.CanAccessStep(step, UserRole.StepUser, 99).Should().BeFalse();
        }

        [Fact]
        public void CanEditProject_Admin_True()
        {
            _service.CanEditProject(_project, UserRole.Admin, 99).Should().BeTrue();
        }

        [Fact]
        public void CanEditProject_User_OwnProject_True()
        {
            _service.CanEditProject(_project, UserRole.User, 2).Should().BeTrue();
        }

        [Fact]
        public void CanEditProject_User_OtherProject_False()
        {
            _service.CanEditProject(_project, UserRole.User, 99).Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_Existing_ReturnsStep()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            var result = await _service.GetByIdAsync(step.Id);
            result.Should().NotBeNull();
            result!.ProjectId.Should().Be(_project.Id);
        }

        [Fact]
        public async Task GetByIdAsync_Deleted_ReturnsNull()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            step.IsDeleted = true;
            await _db.SaveChangesAsync();
            var result = await _service.GetByIdAsync(step.Id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task SoftDeleteAsync_Existing_MarksDeleted()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            var (success, projectId, error) = await _service.SoftDeleteAsync(step.Id, modifiedById: 3);
            success.Should().BeTrue();
            projectId.Should().Be(_project.Id);
            var deleted = await _db.Steps.FindAsync(step.Id);
            deleted!.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task SoftDeleteAsync_NonExisting_Fails()
        {
            var (success, projectId, error) = await _service.SoftDeleteAsync(999, 3);
            success.Should().BeFalse();
            error.Should().Contain("??? ??????");
        }

        [Fact]
        public async Task AddNoteAsync_Valid_Succeeds()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            var (success, error) = await _service.AddNoteAsync(step.Id, "?????? ????", 3);
            success.Should().BeTrue();
            _db.ProgressUpdates.Count(p => p.StepId == step.Id).Should().Be(1);
        }

        [Fact]
        public async Task AddNoteAsync_Empty_Fails()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            var (success, error) = await _service.AddNoteAsync(step.Id, " ", 3);
            success.Should().BeFalse();
            error.Should().Contain("??????");
        }

        [Fact]
        public async Task AddNoteAsync_NonExistingStep_Fails()
        {
            var (success, error) = await _service.AddNoteAsync(999, "??????", 3);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task EditNoteAsync_Valid_Succeeds()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            await _service.AddNoteAsync(step.Id, "?????", 3);
            var note = _db.ProgressUpdates.First(p => p.StepId == step.Id);
            var (success, _) = await _service.EditNoteAsync(note.Id, step.Id, "??????");
            success.Should().BeTrue();
            _db.ProgressUpdates.Find(note.Id)!.NotesAr.Should().Be("??????");
        }

        [Fact]
        public async Task EditNoteAsync_WrongStep_Fails()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            await _service.AddNoteAsync(step.Id, "??????", 3);
            var note = _db.ProgressUpdates.First();
            var (success, _) = await _service.EditNoteAsync(note.Id, stepId: 999, "?????");
            success.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteNoteAsync_Valid_Succeeds()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            await _service.AddNoteAsync(step.Id, "??????", 3);
            var note = _db.ProgressUpdates.First();
            var (success, _) = await _service.DeleteNoteAsync(note.Id, step.Id);
            success.Should().BeTrue();
            _db.ProgressUpdates.Should().BeEmpty();
        }

        [Fact]
        public async Task ApproveStepAsync_PendingStep_Succeeds()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id, weight: 50, progress: 100);
            step.ApprovalStatus = ApprovalStatus.Pending;
            step.Status = StepStatus.InProgress;
            await _db.SaveChangesAsync();
            var (success, error) = await _service.ApproveStepAsync(step.Id, "?????", approverId: 3);
            success.Should().BeTrue();
            var approved = await _db.Steps.FindAsync(step.Id);
            approved!.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
            approved.Status.Should().Be(StepStatus.Completed);
            approved.ApprovedById.Should().Be(3);
        }

        [Fact]
        public async Task ApproveStepAsync_NotPending_Fails()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            step.ApprovalStatus = ApprovalStatus.None;
            await _db.SaveChangesAsync();
            var (success, error) = await _service.ApproveStepAsync(step.Id, null, 3);
            success.Should().BeFalse();
            error.Should().Contain("???? ?????");
        }

        [Fact]
        public async Task RejectStepAsync_PendingStep_Succeeds()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id, weight: 50, progress: 100);
            step.ApprovalStatus = ApprovalStatus.Pending;
            await _db.SaveChangesAsync();
            var (success, error) = await _service.RejectStepAsync(step.Id, "??? ?????", rejecterId: 3);
            success.Should().BeTrue();
            var rejected = await _db.Steps.FindAsync(step.Id);
            rejected!.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
            rejected.ProgressPercentage.Should().Be(99);
            rejected.RejectionReason.Should().Be("??? ?????");
        }

        [Fact]
        public async Task RejectStepAsync_EmptyReason_Fails()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            step.ApprovalStatus = ApprovalStatus.Pending;
            await _db.SaveChangesAsync();
            var (success, error) = await _service.RejectStepAsync(step.Id, "", 3);
            success.Should().BeFalse();
            error.Should().Contain("??? ?????");
        }

        [Fact]
        public async Task RejectStepAsync_NotPending_Fails()
        {
            var step = await TestDbHelper.SeedStepAsync(_db, _project.Id);
            var (success, error) = await _service.RejectStepAsync(step.Id, "???", 3);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task IsStepApproverAsync_NotApprover_ReturnsFalse()
        {
            var result = await _service.IsStepApproverAsync(1);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsStepApproverAsync_Approver_ReturnsTrue()
        {
            var user = await _db.Users.FindAsync(1);
            user!.IsStepApprover = true;
            await _db.SaveChangesAsync();
            var result = await _service.IsStepApproverAsync(1);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsStepApproverAsync_NonExistingUser_ReturnsFalse()
        {
            var result = await _service.IsStepApproverAsync(999);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetPendingApprovalsAsync_ReturnsPendingOnly()
        {
            var step1 = await TestDbHelper.SeedStepAsync(_db, _project.Id, stepNumber: 1);
            step1.ApprovalStatus = ApprovalStatus.Pending;
            var step2 = await TestDbHelper.SeedStepAsync(_db, _project.Id, stepNumber: 2);
            step2.ApprovalStatus = ApprovalStatus.None;
            await _db.SaveChangesAsync();
            var result = await _service.GetPendingApprovalsAsync();
            result.Should().HaveCount(1);
            result.First().ApprovalStatus.Should().Be(ApprovalStatus.Pending);
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_ValidProject_ReturnsViewModel()
        {
            var (viewModel, usedWeight, remainingWeight, projectName, _) =
                await _service.PrepareCreateViewModelAsync(_project.Id);
            viewModel.Should().NotBeNull();
            viewModel!.ProjectId.Should().Be(_project.Id);
            remainingWeight.Should().Be(100);
            projectName.Should().Be("????? ??????");
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_WithExistingSteps_CalculatesRemaining()
        {
            await TestDbHelper.SeedStepAsync(_db, _project.Id, stepNumber: 1, weight: 60);
            var (viewModel, usedWeight, remainingWeight, _, _) =
                await _service.PrepareCreateViewModelAsync(_project.Id);
            usedWeight.Should().Be(60);
            remainingWeight.Should().Be(40);
            viewModel!.StepNumber.Should().Be(2);
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_NonExistingProject_ReturnsNull()
        {
            var (viewModel, _, _, _, _) = await _service.PrepareCreateViewModelAsync(999);
            viewModel.Should().BeNull();
        }
    }
}
