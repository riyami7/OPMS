using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class GuidConversionTests : IDisposable
    {
        private readonly Data.AppDbContext _db;
        private readonly InitiativeService _initiativeService;
        private readonly ProjectService _projectService;

        // Deterministic Guids matching DbSeeder pattern
        private static readonly Guid UnitHQ = Guid.Parse("10000000-0000-0000-0000-000000000100");
        private static readonly Guid UnitOps = Guid.Parse("10000000-0000-0000-0000-000000000101");
        private static readonly Guid UnitIT = Guid.Parse("10000000-0000-0000-0000-000000000102");
        private static readonly Guid UnitDev = Guid.Parse("10000000-0000-0000-0000-000000000110");
        private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public GuidConversionTests()
        {
            _db = TestDbHelper.CreateContext();
            _initiativeService = new InitiativeService(_db, TestDbHelper.CreateLogger<InitiativeService>());
            _projectService = new ProjectService(_db, TestDbHelper.CreateLogger<ProjectService>());
            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            await TestDbHelper.SeedBasicDataAsync(_db);

            // Seed ExternalOrganizationalUnits with Guid IDs
            _db.ExternalOrganizationalUnits.AddRange(
                new ExternalOrganizationalUnit { Id = UnitHQ, ParentId = null, TenantId = TenantId, Code = "HQ", ArabicName = "القيادة العامة", IsActive = true },
                new ExternalOrganizationalUnit { Id = UnitOps, ParentId = UnitHQ, TenantId = TenantId, Code = "OPS", ArabicName = "قسم العمليات", IsActive = true },
                new ExternalOrganizationalUnit { Id = UnitIT, ParentId = UnitHQ, TenantId = TenantId, Code = "IT", ArabicName = "قسم تقنية المعلومات", IsActive = true },
                new ExternalOrganizationalUnit { Id = UnitDev, ParentId = UnitIT, TenantId = TenantId, Code = "DEV", ArabicName = "شعبة التطوير", IsActive = true }
            );
            await _db.SaveChangesAsync();
        }

        public void Dispose() => _db.Dispose();

        // ============================================================
        // ExternalOrganizationalUnit Entity Tests
        // ============================================================

        [Fact]
        public void ExternalUnit_Id_IsGuid()
        {
            var unit = _db.ExternalOrganizationalUnits.Find(UnitHQ);
            unit.Should().NotBeNull();
            unit!.Id.Should().Be(UnitHQ);
            unit.Id.GetType().Should().Be(typeof(Guid));
        }

        [Fact]
        public void ExternalUnit_ParentId_IsNullableGuid()
        {
            var root = _db.ExternalOrganizationalUnits.Find(UnitHQ);
            root!.ParentId.Should().BeNull();

            var child = _db.ExternalOrganizationalUnits.Find(UnitOps);
            child!.ParentId.Should().Be(UnitHQ);
        }

        [Fact]
        public void ExternalUnit_IsRoot_WorksWithoutIntComparison()
        {
            var root = _db.ExternalOrganizationalUnits.Find(UnitHQ);
            root!.IsRoot.Should().BeTrue();

            var child = _db.ExternalOrganizationalUnits.Find(UnitOps);
            child!.IsRoot.Should().BeFalse();
        }

        [Fact]
        public async Task ExternalUnit_HierarchyQuery_WorksWithGuid()
        {
            var children = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == UnitHQ && u.IsActive)
                .ToListAsync();

            children.Should().HaveCount(2); // OPS + IT
            children.Select(c => c.Code).Should().Contain("OPS").And.Contain("IT");
        }

        [Fact]
        public async Task ExternalUnit_ThreeLevelHierarchy_Works()
        {
            // Level 1: HQ → Level 2: IT → Level 3: DEV
            var level3 = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == UnitIT)
                .ToListAsync();

            level3.Should().HaveCount(1);
            level3.First().Code.Should().Be("DEV");
        }

        // ============================================================
        // Initiative with Guid ExternalUnitId
        // ============================================================

        [Fact]
        public async Task Initiative_ExternalUnitId_AcceptsGuid()
        {
            var initiative = new Initiative
            {
                Code = "INI-GUID-001", NameAr = "مبادرة Guid", NameEn = "Guid Initiative",
                FiscalYearId = 1, SupervisorId = 1, CreatedById = 3, CreatedAt = DateTime.Now,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6),
                ExternalUnitId = UnitOps, ExternalUnitName = "قسم العمليات"
            };
            _db.Initiatives.Add(initiative);
            await _db.SaveChangesAsync();

            var saved = await _db.Initiatives.FindAsync(initiative.Id);
            saved!.ExternalUnitId.Should().Be(UnitOps);
        }

        [Fact]
        public async Task Initiative_ExternalUnitId_NullableWorks()
        {
            var initiative = new Initiative
            {
                Code = "INI-NULL-001", NameAr = "بدون وحدة", NameEn = "No Unit",
                FiscalYearId = 1, SupervisorId = 1, CreatedById = 3, CreatedAt = DateTime.Now,
                Status = Status.Draft, Priority = Priority.Low,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(3),
                ExternalUnitId = null
            };
            _db.Initiatives.Add(initiative);
            await _db.SaveChangesAsync();

            var saved = await _db.Initiatives.FindAsync(initiative.Id);
            saved!.ExternalUnitId.Should().BeNull();
        }

        // ============================================================
        // GetUnitNameAsync with Guid
        // ============================================================

        [Fact]
        public async Task GetUnitNameAsync_ExistingGuid_ReturnsName()
        {
            var name = await _initiativeService.GetUnitNameAsync(UnitOps);
            name.Should().Be("قسم العمليات");
        }

        [Fact]
        public async Task GetUnitNameAsync_NonExistingGuid_ReturnsNull()
        {
            var name = await _initiativeService.GetUnitNameAsync(Guid.NewGuid());
            name.Should().BeNull();
        }

        // ============================================================
        // GetListAsync filtering by Guid ExternalUnitId
        // ============================================================

        [Fact]
        public async Task GetListAsync_FilterByGuidUnit_ReturnsMatching()
        {
            // Seed 2 initiatives in different units
            _db.Initiatives.AddRange(
                new Initiative { Code = "INI-OPS", NameAr = "عمليات", FiscalYearId = 1, SupervisorId = 1, CreatedById = 3, Status = Status.InProgress, Priority = Priority.Medium, PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6), ExternalUnitId = UnitOps },
                new Initiative { Code = "INI-IT", NameAr = "تقنية", FiscalYearId = 1, SupervisorId = 1, CreatedById = 3, Status = Status.InProgress, Priority = Priority.Medium, PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6), ExternalUnitId = UnitIT }
            );
            await _db.SaveChangesAsync();

            var result = await _initiativeService.GetListAsync(null, null, UnitOps, 1, 20, UserRole.Admin, 3);
            result.Initiatives.Should().HaveCount(1);
            result.Initiatives.First().Code.Should().Be("INI-OPS");
        }

        [Fact]
        public async Task GetListAsync_FilterByParentUnit_IncludesChildren()
        {
            // Initiative in child unit (DEV is child of IT)
            _db.Initiatives.Add(new Initiative
            {
                Code = "INI-DEV", NameAr = "تطوير", FiscalYearId = 1, SupervisorId = 1, CreatedById = 3,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6),
                ExternalUnitId = UnitDev
            });
            await _db.SaveChangesAsync();

            // Filter by IT (parent) should include DEV (child)
            var result = await _initiativeService.GetListAsync(null, null, UnitIT, 1, 20, UserRole.Admin, 3);
            result.Initiatives.Should().HaveCountGreaterOrEqualTo(1);
        }

        // ============================================================
        // SubObjectivesByUnit with Guid
        // ============================================================

        [Fact]
        public async Task GetSubObjectivesByUnit_WithGuid_ReturnsMatching()
        {
            _db.SubObjectives.Add(new SubObjective
            {
                Code = "SUB-IT", NameAr = "هدف تقنية", MainObjectiveId = 0,
                ExternalUnitId = UnitIT, IsActive = true, OrderIndex = 1, CreatedById = 3
            });
            await _db.SaveChangesAsync();

            var result = await _projectService.GetSubObjectivesByUnitAsync(UnitIT);
            var list = (result as IEnumerable<object>)!.ToList();
            list.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetSubObjectivesByUnit_NullGuid_ReturnsEmpty()
        {
            var result = await _projectService.GetSubObjectivesByUnitAsync(null);
            var list = (result as IEnumerable<object>)!.ToList();
            list.Should().BeEmpty();
        }

        [Fact]
        public async Task GetSubObjectivesByUnit_NonExistingGuid_ReturnsEmpty()
        {
            var result = await _projectService.GetSubObjectivesByUnitAsync(Guid.NewGuid());
            var list = (result as IEnumerable<object>)!.ToList();
            list.Should().BeEmpty();
        }

        // ============================================================
        // SupportingEntityDisplayItem.Id as string
        // ============================================================

        [Fact]
        public void SupportingEntityDisplayItem_Id_IsString()
        {
            var item = new SupportingEntityDisplayItem { Id = UnitOps.ToString(), NameAr = "جهة" };
            item.Id.Should().Be(UnitOps.ToString());

            var item2 = new SupportingEntityDisplayItem { Id = "42", NameAr = "جهة محلية" };
            item2.Id.Should().Be("42");
        }

        // ============================================================
        // SubObjectiveId backward compat fix (null vs 0)
        // ============================================================

        [Fact]
        public void ProjectFormViewModel_SubObjectiveId_NullWhenEmpty()
        {
            var vm = new ProjectFormViewModel { SubObjectiveIds = new List<int>() };
            var entity = new Project
            {
                Code = "X", NameAr = "X", InitiativeId = 1, CreatedById = 3,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(1)
            };

            vm.UpdateEntity(entity);
            entity.SubObjectiveId.Should().BeNull("empty SubObjectiveIds should map to null, not 0");
        }

        [Fact]
        public void ProjectFormViewModel_SubObjectiveId_SetWhenPresent()
        {
            var vm = new ProjectFormViewModel { SubObjectiveIds = new List<int> { 5, 3 } };
            var entity = new Project
            {
                Code = "X", NameAr = "X", InitiativeId = 1, CreatedById = 3,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(1)
            };

            vm.UpdateEntity(entity);
            entity.SubObjectiveId.Should().Be(5, "should take first SubObjectiveId for backward compat");
        }

        // ============================================================
        // Project with Guid ExternalUnitId
        // ============================================================

        [Fact]
        public async Task Project_ExternalUnitId_AcceptsGuid()
        {
            var initiative = await TestDbHelper.SeedInitiativeAsync(_db);
            var project = new Project
            {
                Code = "PRJ-GUID", ProjectNumber = "P-GUID", NameAr = "مشروع Guid",
                InitiativeId = initiative.Id, ProjectManagerId = 2, CreatedById = 3,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(3),
                ExternalUnitId = UnitDev, ExternalUnitName = "شعبة التطوير"
            };
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            var saved = await _db.Projects.FindAsync(project.Id);
            saved!.ExternalUnitId.Should().Be(UnitDev);
        }

        // ============================================================
        // User with Guid ExternalUnitId
        // ============================================================

        [Fact]
        public async Task User_ExternalUnitId_AcceptsGuid()
        {
            var user = await _db.Users.FindAsync(1);
            user!.ExternalUnitId = UnitOps;
            await _db.SaveChangesAsync();

            var saved = await _db.Users.FindAsync(1);
            saved!.ExternalUnitId.Should().Be(UnitOps);
        }

        [Fact]
        public async Task User_ExternalUnitId_NullWorks()
        {
            var user = await _db.Users.FindAsync(1);
            user!.ExternalUnitId = null;
            await _db.SaveChangesAsync();

            var saved = await _db.Users.FindAsync(1);
            saved!.ExternalUnitId.Should().BeNull();
        }

        // ============================================================
        // ProjectSupportingUnit with Guid ExternalUnitId
        // ============================================================

        [Fact]
        public async Task ProjectSupportingUnit_GuidExternalUnitId_Works()
        {
            var initiative = await TestDbHelper.SeedInitiativeAsync(_db, code: "INI-SUP");
            var project = await TestDbHelper.SeedProjectAsync(_db, initiative.Id, code: "PRJ-SUP");

            var psu = new ProjectSupportingUnit
            {
                ProjectId = project.Id,
                ExternalUnitId = UnitOps,
                ExternalUnitName = "قسم العمليات"
            };
            _db.ProjectSupportingUnits.Add(psu);
            await _db.SaveChangesAsync();

            var saved = await _db.ProjectSupportingUnits.FirstAsync(s => s.ProjectId == project.Id);
            saved.ExternalUnitId.Should().Be(UnitOps);
        }

        // ============================================================
        // OrganizationalUnitSettings with Guid
        // ============================================================

        [Fact]
        public async Task UnitSettings_GuidExternalUnitId_Works()
        {
            var settings = new OrganizationalUnitSettings
            {
                ExternalUnitId = UnitIT,
                ExternalUnitName = "قسم تقنية المعلومات",
                VisionAr = "رؤية",
                MissionAr = "رسالة",
                CreatedById = 3
            };
            _db.OrganizationalUnitSettings.Add(settings);
            await _db.SaveChangesAsync();

            var saved = await _db.OrganizationalUnitSettings.FirstAsync(s => s.ExternalUnitId == UnitIT);
            saved.ExternalUnitId.Should().Be(UnitIT);
        }

        // ============================================================
        // ProgressPercentage as int in forms
        // ============================================================

        [Fact]
        public void Step_ProgressPercentage_IntCastWorks()
        {
            var step = new Step { ProgressPercentage = 100.00m };
            var intValue = (int)step.ProgressPercentage;
            intValue.Should().Be(100);
        }

        [Fact]
        public void Step_ProgressPercentage_ZeroCastWorks()
        {
            var step = new Step { ProgressPercentage = 0m };
            var intValue = (int)step.ProgressPercentage;
            intValue.Should().Be(0);
        }
    }
}
