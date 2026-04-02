using FluentAssertions;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Services;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class InitiativeServiceTests : IDisposable
    {
        private readonly Data.AppDbContext _db;
        private readonly InitiativeService _service;

        public InitiativeServiceTests()
        {
            _db = TestDbHelper.CreateContext();
            _service = new InitiativeService(_db, TestDbHelper.CreateLogger<InitiativeService>(), TestDbHelper.CreateAuditService(), TestDbHelper.CreateUserService());
            TestDbHelper.SeedBasicDataAsync(_db).GetAwaiter().GetResult();
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public void CanAccess_Admin_AlwaysTrue()
        {
            var initiative = new Initiative { SupervisorId = 1 };
            _service.CanAccess(initiative, UserRole.Admin, userId: 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_Executive_AlwaysTrue()
        {
            var initiative = new Initiative { SupervisorId = 1 };
            _service.CanAccess(initiative, UserRole.Executive, userId: 99).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_Supervisor_OwnInitiative_True()
        {
            var initiative = new Initiative { SupervisorId = 1 };
            _service.CanAccess(initiative, UserRole.Supervisor, userId: 1).Should().BeTrue();
        }

        [Fact]
        public void CanAccess_Supervisor_OtherInitiative_False()
        {
            var initiative = new Initiative { SupervisorId = 1 };
            _service.CanAccess(initiative, UserRole.Supervisor, userId: 99).Should().BeFalse();
        }

        [Fact]
        public void CanAccess_User_AlwaysFalse()
        {
            var initiative = new Initiative { SupervisorId = 1 };
            _service.CanAccess(initiative, UserRole.User, userId: 1).Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_Existing_ReturnsInitiative()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            var result = await _service.GetByIdAsync(seeded.Id);
            result.Should().NotBeNull();
            result!.Code.Should().Be("INI-2026-001");
        }

        [Fact]
        public async Task GetByIdAsync_Deleted_ReturnsNull()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            seeded.IsDeleted = true;
            await _db.SaveChangesAsync();
            var result = await _service.GetByIdAsync(seeded.Id);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_NonExisting_ReturnsNull()
        {
            var result = await _service.GetByIdAsync(999);
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_ValidModel_Succeeds()
        {
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = "INI-2026-100", NameAr = "?????? ?????", NameEn = "New Initiative",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, id, error) = await _service.CreateAsync(model, createdById: 3);
            success.Should().BeTrue();
            id.Should().BeGreaterThan(0);
            error.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_DuplicateCode_Fails()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, code: "INI-2026-001");
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = "INI-2026-001", NameAr = "?????", NameEn = "Duplicate",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, id, error) = await _service.CreateAsync(model, createdById: 3);
            success.Should().BeFalse();
            error.Should().Contain("?????? ??????");
        }

        [Fact]
        public async Task UpdateAsync_Admin_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = seeded.Code, NameAr = "?????? ?????", NameEn = "Updated",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, error) = await _service.UpdateAsync(seeded.Id, model, modifiedById: 3, UserRole.Admin, 3);
            success.Should().BeTrue();
            var updated = await _service.GetByIdAsync(seeded.Id);
            updated!.NameAr.Should().Be("?????? ?????");
        }

        [Fact]
        public async Task UpdateAsync_Supervisor_OwnInitiative_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1);
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = seeded.Code, NameAr = "????? ??????", NameEn = "Supervisor Edit",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, error) = await _service.UpdateAsync(seeded.Id, model, modifiedById: 1, UserRole.Supervisor, 1);
            success.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateAsync_Supervisor_OtherInitiative_Fails()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1);
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = seeded.Code, NameAr = "??????", NameEn = "Attempt",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, error) = await _service.UpdateAsync(seeded.Id, model, modifiedById: 99, UserRole.Supervisor, 99);
            success.Should().BeFalse();
            error.Should().Contain("?? ?????");
        }

        [Fact]
        public async Task UpdateAsync_NonExisting_Fails()
        {
            var model = new Models.ViewModels.InitiativeFormViewModel
            {
                Code = "INI-XXXX", NameAr = "X", NameEn = "X",
                FiscalYearId = 1, SupervisorId = 1,

            };
            var (success, error) = await _service.UpdateAsync(999, model, 3, UserRole.Admin, 3);
            success.Should().BeFalse();
            error.Should().Contain("??? ??????");
        }

        [Fact]
        public async Task SoftDeleteAsync_Admin_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            var (success, error) = await _service.SoftDeleteAsync(seeded.Id, modifiedById: 3, UserRole.Admin, 3);
            success.Should().BeTrue();
            var deleted = await _db.Initiatives.FindAsync(seeded.Id);
            deleted!.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task SoftDeleteAsync_Supervisor_OwnInitiative_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1);
            var (success, error) = await _service.SoftDeleteAsync(seeded.Id, 1, UserRole.Supervisor, 1);
            success.Should().BeTrue();
        }

        [Fact]
        public async Task SoftDeleteAsync_Supervisor_OtherInitiative_Fails()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1);
            var (success, error) = await _service.SoftDeleteAsync(seeded.Id, 99, UserRole.Supervisor, 99);
            success.Should().BeFalse();
            error.Should().Contain("?? ?????");
        }

        [Fact]
        public async Task AddNoteAsync_Valid_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            var (success, error) = await _service.AddNoteAsync(seeded.Id, "?????? ???????", createdById: 3);
            success.Should().BeTrue();
            _db.ProgressUpdates.Count(p => p.InitiativeId == seeded.Id).Should().Be(1);
        }

        [Fact]
        public async Task AddNoteAsync_EmptyNote_Fails()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            var (success, error) = await _service.AddNoteAsync(seeded.Id, "  ", createdById: 3);
            success.Should().BeFalse();
            error.Should().Contain("??????");
        }

        [Fact]
        public async Task AddNoteAsync_NonExistingInitiative_Fails()
        {
            var (success, error) = await _service.AddNoteAsync(999, "??????", createdById: 3);
            success.Should().BeFalse();
            error.Should().Contain("??? ??????");
        }

        [Fact]
        public async Task EditNoteAsync_Valid_Succeeds()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            await _service.AddNoteAsync(seeded.Id, "?????", 3);
            var note = _db.ProgressUpdates.First(p => p.InitiativeId == seeded.Id);
            var (success, error) = await _service.EditNoteAsync(note.Id, seeded.Id, "??????");
            success.Should().BeTrue();
            _db.ProgressUpdates.Find(note.Id)!.NotesAr.Should().Be("??????");
        }

        [Fact]
        public async Task DeleteNoteAsync_Valid_RemovesFromDb()
        {
            var seeded = await TestDbHelper.SeedInitiativeAsync(_db);
            await _service.AddNoteAsync(seeded.Id, "?????? ?????", 3);
            var note = _db.ProgressUpdates.First();
            var (success, error) = await _service.DeleteNoteAsync(note.Id, seeded.Id);
            success.Should().BeTrue();
            _db.ProgressUpdates.Should().BeEmpty();
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_FirstInitiative_GeneratesCode001()
        {
            var result = await _service.PrepareCreateViewModelAsync();
            result.Code.Should().MatchRegex(@"INI-\d{4}-001");
            result.FiscalYears.Should().NotBeNull();
        }

        [Fact]
        public async Task PrepareCreateViewModelAsync_AfterExisting_IncrementsCode()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, code: $"INI-{DateTime.Now.Year}-005");
            var result = await _service.PrepareCreateViewModelAsync();
            result.Code.Should().EndWith("006");
        }

        [Fact]
        public async Task GetListAsync_Admin_SeesAll()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1, code: "INI-2026-001");
            await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 2, code: "INI-2026-002");
            var result = await _service.GetListAsync(null, null, null, 1, 20, UserRole.Admin, 3);
            result.Initiatives.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetListAsync_Supervisor_SeesOnlyOwn()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 1, code: "INI-2026-001");
            await TestDbHelper.SeedInitiativeAsync(_db, supervisorId: 2, code: "INI-2026-002");
            var result = await _service.GetListAsync(null, null, null, 1, 20, UserRole.Supervisor, 1);
            result.Initiatives.Should().HaveCount(1);
            result.Initiatives.First().SupervisorId.Should().Be(1);
        }

        [Fact]
        public async Task GetListAsync_SearchByCode_Filters()
        {
            await TestDbHelper.SeedInitiativeAsync(_db, code: "INI-2026-001");
            await TestDbHelper.SeedInitiativeAsync(_db, code: "INI-2026-002");
            var result = await _service.GetListAsync("002", null, null, 1, 20, UserRole.Admin, 3);
            result.Initiatives.Should().HaveCount(1);
        }
    }
}
