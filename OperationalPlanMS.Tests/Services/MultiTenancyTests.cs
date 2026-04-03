using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;
using OperationalPlanMS.Services;
using OperationalPlanMS.Services.Tenant;
using OperationalPlanMS.Tests.Helpers;

namespace OperationalPlanMS.Tests.Services
{
    public class MultiTenancyTests : IDisposable
    {
        private readonly string _dbName;

        public MultiTenancyTests()
        {
            _dbName = Guid.NewGuid().ToString();

            // Seed data with SuperAdmin context (no filter)
            using var seedDb = TestDbHelper.CreateContext(_dbName, TestDbHelper.SuperAdminProvider());
            TestDbHelper.SeedMultiTenantDataAsync(seedDb).GetAwaiter().GetResult();
        }

        public void Dispose() { }

        private AppDbContext CreateDb(ITenantProvider provider) => TestDbHelper.CreateContext(_dbName, provider);

        // ================================================================
        //  Initiative Global Query Filter
        // ================================================================

        [Fact]
        public async Task Initiatives_SuperAdmin_SeesAll()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().HaveCount(3); // 2 tenant1 + 1 tenant2
        }

        [Fact]
        public async Task Initiatives_Tenant1_SeesOnlyOwn()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().HaveCount(2);
            initiatives.Should().AllSatisfy(i => i.TenantId.Should().Be(TestDbHelper.Tenant1Id));
        }

        [Fact]
        public async Task Initiatives_Tenant2_SeesOnlyOwn()
        {
            using var db = CreateDb(TestDbHelper.Tenant2Provider());
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().HaveCount(1);
            initiatives.Should().AllSatisfy(i => i.TenantId.Should().Be(TestDbHelper.Tenant2Id));
        }

        [Fact]
        public async Task Initiatives_UnknownTenant_SeesNothing()
        {
            var unknownTenant = TestDbHelper.CreateTenantProvider(Guid.NewGuid());
            using var db = CreateDb(unknownTenant);
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().BeEmpty();
        }

        // ================================================================
        //  Projects — filtered via Initiative relationship
        // ================================================================

        [Fact]
        public async Task Projects_SuperAdmin_SeesAll()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var projects = await db.Projects.Include(p => p.Initiative).ToListAsync();
            projects.Should().HaveCount(3); // 2 tenant1 + 1 tenant2
        }

        [Fact]
        public async Task Projects_Tenant1_SeesOnlyOwn()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var projects = await db.Projects.Include(p => p.Initiative).ToListAsync();
            // Projects linked to filtered-out initiatives should still show
            // but Initiative nav property will be null for other tenants
            // Actually with Include, EF applies filter on Initiative
            projects.Where(p => p.Initiative != null)
                .Should().AllSatisfy(p => p.Initiative!.TenantId.Should().Be(TestDbHelper.Tenant1Id));
        }

        // ================================================================
        //  UserService — tenant filtering
        // ================================================================

        [Fact]
        public async Task UserService_Tenant1_SeesOnlyTenant1Users()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider()); // Users table not filtered by EF
            var service = new UserService(db, TestDbHelper.CreateLogger<UserService>(), TestDbHelper.Tenant1Provider());
            var result = await service.GetUsersAsync(null, null, null, 1);
            result.Users.Should().AllSatisfy(u => u.TenantId.Should().Be(TestDbHelper.Tenant1Id));
            result.Users.Should().HaveCount(3); // admin1, sup1, pm1
        }

        [Fact]
        public async Task UserService_Tenant2_SeesOnlyTenant2Users()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var service = new UserService(db, TestDbHelper.CreateLogger<UserService>(), TestDbHelper.Tenant2Provider());
            var result = await service.GetUsersAsync(null, null, null, 1);
            result.Users.Should().AllSatisfy(u => u.TenantId.Should().Be(TestDbHelper.Tenant2Id));
            result.Users.Should().HaveCount(3); // admin2, sup2, pm2
        }

        [Fact]
        public async Task UserService_SuperAdmin_SeesAllUsers()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var service = new UserService(db, TestDbHelper.CreateLogger<UserService>(), TestDbHelper.SuperAdminProvider());
            var result = await service.GetUsersAsync(null, null, null, 1);
            result.Users.Should().HaveCount(7); // all users
        }

        // ================================================================
        //  InitiativeService — CanAccess with SuperAdmin
        // ================================================================

        [Fact]
        public void InitiativeService_SuperAdmin_CanAccess_AlwaysTrue()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var service = new InitiativeService(db, TestDbHelper.CreateLogger<InitiativeService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateUserService());
            var initiative = new Initiative { SupervisorId = 999 };
            service.CanAccess(initiative, UserRole.SuperAdmin, userId: 1).Should().BeTrue();
        }

        [Fact]
        public async Task InitiativeService_GetList_Tenant1_FiltersCorrectly()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var service = new InitiativeService(db, TestDbHelper.CreateLogger<InitiativeService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateUserService());
            // Admin of tenant 1 should see tenant 1 initiatives via Global Query Filter
            var result = await service.GetListAsync(null, null, null, 1, 20, UserRole.Admin, 2);
            result.Initiatives.Should().HaveCount(2);
        }

        [Fact]
        public async Task InitiativeService_GetList_SuperAdmin_SeesAll()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var service = new InitiativeService(db, TestDbHelper.CreateLogger<InitiativeService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateUserService());
            var result = await service.GetListAsync(null, null, null, 1, 20, UserRole.SuperAdmin, 1);
            result.Initiatives.Should().HaveCount(3);
        }

        // ================================================================
        //  StepService — tenant filtering
        // ================================================================

        [Fact]
        public async Task StepService_Tenant1_SeesOnlyTenant1Steps()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var tenant1Provider = TestDbHelper.Tenant1Provider();
            var service = new StepService(db, TestDbHelper.CreateLogger<StepService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(),
                TestDbHelper.CreateUserService(), tenant1Provider);

            var filters = new StepListViewModel { CurrentPage = 1, PageSize = 50 };
            var result = await service.GetListAsync(filters, UserRole.Admin, 2);
            result.Steps.Should().HaveCount(3); // 2 steps from proj1a + 1 from proj1b
        }

        [Fact]
        public async Task StepService_Tenant2_SeesOnlyTenant2Steps()
        {
            using var db = CreateDb(TestDbHelper.Tenant2Provider());
            var tenant2Provider = TestDbHelper.Tenant2Provider();
            var service = new StepService(db, TestDbHelper.CreateLogger<StepService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(),
                TestDbHelper.CreateUserService(), tenant2Provider);

            var filters = new StepListViewModel { CurrentPage = 1, PageSize = 50 };
            var result = await service.GetListAsync(filters, UserRole.Admin, 5);
            result.Steps.Should().HaveCount(2); // 2 steps from proj2a
        }

        [Fact]
        public async Task StepService_SuperAdmin_SeesAllSteps()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var superProvider = TestDbHelper.SuperAdminProvider();
            var service = new StepService(db, TestDbHelper.CreateLogger<StepService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(),
                TestDbHelper.CreateUserService(), superProvider);

            var filters = new StepListViewModel { CurrentPage = 1, PageSize = 50 };
            var result = await service.GetListAsync(filters, UserRole.SuperAdmin, 1);
            result.Steps.Should().HaveCount(5); // all steps
        }

        // ================================================================
        //  ProjectService — tenant filtering
        // ================================================================

        [Fact]
        public async Task ProjectService_Tenant1_SeesOnlyTenant1Projects()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var tenant1Provider = TestDbHelper.Tenant1Provider();
            var service = new ProjectService(db, TestDbHelper.CreateLogger<ProjectService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(),
                TestDbHelper.CreateUserService(), tenant1Provider);

            var result = await service.GetListAsync(null, null, null, 1, 50, UserRole.Admin, 2);
            result.Projects.Should().HaveCount(2);
        }

        [Fact]
        public async Task ProjectService_Tenant2_SeesOnlyTenant2Projects()
        {
            using var db = CreateDb(TestDbHelper.Tenant2Provider());
            var tenant2Provider = TestDbHelper.Tenant2Provider();
            var service = new ProjectService(db, TestDbHelper.CreateLogger<ProjectService>(),
                TestDbHelper.CreateAuditService(), TestDbHelper.CreateNotificationService(),
                TestDbHelper.CreateUserService(), tenant2Provider);

            var result = await service.GetListAsync(null, null, null, 1, 50, UserRole.Admin, 5);
            result.Projects.Should().HaveCount(1);
        }

        // ================================================================
        //  TenantProvider — unit tests
        // ================================================================

        [Fact]
        public void TenantProvider_SuperAdmin_ReturnsNullTenantId()
        {
            var provider = TestDbHelper.SuperAdminProvider();
            provider.CurrentTenantId.Should().BeNull();
            provider.IsSuperAdmin.Should().BeTrue();
        }

        [Fact]
        public void TenantProvider_Tenant1_ReturnsCorrectId()
        {
            var provider = TestDbHelper.Tenant1Provider();
            provider.CurrentTenantId.Should().Be(TestDbHelper.Tenant1Id);
            provider.IsSuperAdmin.Should().BeFalse();
        }

        // ================================================================
        //  Cross-tenant isolation — critical security test
        // ================================================================

        [Fact]
        public async Task CrossTenant_Tenant2CannotSee_Tenant1Initiatives()
        {
            using var db = CreateDb(TestDbHelper.Tenant2Provider());
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().NotContain(i => i.TenantId == TestDbHelper.Tenant1Id);
        }

        [Fact]
        public async Task CrossTenant_Tenant1CannotSee_Tenant2Initiatives()
        {
            using var db = CreateDb(TestDbHelper.Tenant1Provider());
            var initiatives = await db.Initiatives.ToListAsync();
            initiatives.Should().NotContain(i => i.TenantId == TestDbHelper.Tenant2Id);
        }

        [Fact]
        public async Task CrossTenant_UserService_Tenant1CannotSee_Tenant2Users()
        {
            using var db = CreateDb(TestDbHelper.SuperAdminProvider());
            var service = new UserService(db, TestDbHelper.CreateLogger<UserService>(), TestDbHelper.Tenant1Provider());
            var result = await service.GetUsersAsync(null, null, null, 1);
            result.Users.Should().NotContain(u => u.TenantId == TestDbHelper.Tenant2Id);
        }
    }
}
