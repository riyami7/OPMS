using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OperationalPlanMS.Services;
using OperationalPlanMS.Services.Tenant;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Tests.Helpers
{
    public static class TestDbHelper
    {
        // Tenant IDs
        public static readonly Guid Tenant1Id = Guid.Parse("10000000-0000-0000-0000-000000000100");
        public static readonly Guid Tenant2Id = Guid.Parse("20000000-0000-0000-0000-000000000200");

        /// <summary>
        /// Creates DbContext with optional tenant filtering
        /// </summary>
        public static AppDbContext CreateContext(string? dbName = null, ITenantProvider? tenantProvider = null)
        {
            dbName ??= Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options, tenantProvider);
        }

        /// <summary>
        /// Creates a mock ITenantProvider for a specific tenant
        /// </summary>
        public static ITenantProvider CreateTenantProvider(Guid? tenantId, bool isSuperAdmin = false)
        {
            var mock = new Mock<ITenantProvider>();
            mock.Setup(t => t.CurrentTenantId).Returns(tenantId);
            mock.Setup(t => t.IsSuperAdmin).Returns(isSuperAdmin);
            return mock.Object;
        }

        /// <summary>SuperAdmin — sees all tenants</summary>
        public static ITenantProvider SuperAdminProvider() => CreateTenantProvider(null, isSuperAdmin: true);

        /// <summary>Tenant 1 admin</summary>
        public static ITenantProvider Tenant1Provider() => CreateTenantProvider(Tenant1Id);

        /// <summary>Tenant 2 admin</summary>
        public static ITenantProvider Tenant2Provider() => CreateTenantProvider(Tenant2Id);

        public static IAuditService CreateAuditService() => new Mock<IAuditService>().Object;
        public static INotificationService CreateNotificationService() => new Mock<INotificationService>().Object;
        public static IUserService CreateUserService() => new Mock<IUserService>().Object;
        public static ILogger<T> CreateLogger<T>() => new Mock<ILogger<T>>().Object;

        /// <summary>
        /// Seeds roles, users, fiscal year — with TenantId support
        /// </summary>
        public static async Task SeedBasicDataAsync(AppDbContext db)
        {
            if (!db.Roles.Any())
            {
                db.Roles.AddRange(
                    new Role { Id = 1, Code = "admin", NameAr = "مدير الوحدة", NameEn = "Admin" },
                    new Role { Id = 2, Code = "executive", NameAr = "تنفيذي", NameEn = "Executive" },
                    new Role { Id = 3, Code = "supervisor", NameAr = "مشرف", NameEn = "Supervisor" },
                    new Role { Id = 4, Code = "user", NameAr = "مدير مشروع", NameEn = "User" },
                    new Role { Id = 7, Code = "stepuser", NameAr = "منفذ خطوة", NameEn = "Step User" },
                    new Role { Id = 8, Code = "superadmin", NameAr = "مدير النظام الأعلى", NameEn = "Super Admin" }
                );
                await db.SaveChangesAsync();
            }

            if (!db.Users.Any())
            {
                db.Users.AddRange(
                    // SuperAdmin — no tenant
                    new User { Id = 1, ADUsername = "superadmin", FullNameAr = "مدير النظام", FullNameEn = "Super Admin", RoleId = 8, IsActive = true, TenantId = null },
                    // Tenant 1 users
                    new User { Id = 2, ADUsername = "admin1", FullNameAr = "أحمد المدير", FullNameEn = "Ahmed Admin", RoleId = 1, IsActive = true, TenantId = Tenant1Id },
                    new User { Id = 3, ADUsername = "sup1", FullNameAr = "سالم المشرف", FullNameEn = "Salem Supervisor", RoleId = 3, IsActive = true, TenantId = Tenant1Id },
                    new User { Id = 4, ADUsername = "pm1", FullNameAr = "محمد المدير", FullNameEn = "Mohammed PM", RoleId = 4, IsActive = true, TenantId = Tenant1Id },
                    // Tenant 2 users
                    new User { Id = 5, ADUsername = "admin2", FullNameAr = "عبدالله المدير", FullNameEn = "Abdullah Admin", RoleId = 1, IsActive = true, TenantId = Tenant2Id },
                    new User { Id = 6, ADUsername = "sup2", FullNameAr = "خالد المشرف", FullNameEn = "Khalid Supervisor", RoleId = 3, IsActive = true, TenantId = Tenant2Id },
                    new User { Id = 7, ADUsername = "pm2", FullNameAr = "يوسف المدير", FullNameEn = "Yousef PM", RoleId = 4, IsActive = true, TenantId = Tenant2Id }
                );
                await db.SaveChangesAsync();
            }

            if (!db.FiscalYears.Any())
            {
                db.FiscalYears.Add(new FiscalYear
                {
                    Id = 1, Year = 2026, NameAr = "2026", NameEn = "2026",
                    StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsCurrent = true
                });
                await db.SaveChangesAsync();
            }
        }

        /// <summary>Seeds an initiative for a specific tenant</summary>
        public static async Task<Initiative> SeedInitiativeAsync(AppDbContext db, Guid? tenantId = null, int? supervisorId = 3, string code = "INI-2026-001")
        {
            var initiative = new Initiative
            {
                Code = code, NameAr = "مبادرة اختبارية", NameEn = "Test Initiative",
                FiscalYearId = 1, SupervisorId = supervisorId, CreatedById = 1, CreatedAt = DateTime.Now,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6),
                TenantId = tenantId
            };
            db.Initiatives.Add(initiative);
            await db.SaveChangesAsync();
            return initiative;
        }

        /// <summary>Seeds a project under an initiative</summary>
        public static async Task<Project> SeedProjectAsync(AppDbContext db, int initiativeId, int? managerId = 4, string code = "PRJ-2026-001")
        {
            var project = new Project
            {
                Code = code, ProjectNumber = $"P-{code}", NameAr = "مشروع اختباري", NameEn = "Test Project",
                InitiativeId = initiativeId, ProjectManagerId = managerId, CreatedById = 1, CreatedAt = DateTime.Now,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(3)
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        }

        /// <summary>Seeds a step under a project</summary>
        public static async Task<Step> SeedStepAsync(
            AppDbContext db, int projectId, int? initiativeId = null, int stepNumber = 1,
            decimal weight = 50, decimal progress = 0,
            int? assignedToId = 4, StepStatus status = StepStatus.NotStarted)
        {
            var step = new Step
            {
                StepNumber = stepNumber, NameAr = $"خطوة اختبارية {stepNumber}", NameEn = $"Test Step {stepNumber}",
                ProjectId = projectId, InitiativeId = initiativeId, Weight = weight, ProgressPercentage = progress, Status = status,
                AssignedToId = assignedToId, CreatedById = 1, CreatedAt = DateTime.Now,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddDays(30)
            };
            db.Steps.Add(step);
            await db.SaveChangesAsync();
            return step;
        }

        /// <summary>
        /// Seeds complete multi-tenant data: 2 initiatives per tenant, each with 1 project and 2 steps
        /// </summary>
        public static async Task SeedMultiTenantDataAsync(AppDbContext db)
        {
            await SeedBasicDataAsync(db);

            // Tenant 1: 2 initiatives
            var init1a = await SeedInitiativeAsync(db, Tenant1Id, supervisorId: 3, code: "T1-INI-001");
            var init1b = await SeedInitiativeAsync(db, Tenant1Id, supervisorId: 3, code: "T1-INI-002");
            var proj1a = await SeedProjectAsync(db, init1a.Id, managerId: 4, code: "T1-PRJ-001");
            var proj1b = await SeedProjectAsync(db, init1b.Id, managerId: 4, code: "T1-PRJ-002");
            await SeedStepAsync(db, proj1a.Id, init1a.Id, 1, assignedToId: 4);
            await SeedStepAsync(db, proj1a.Id, init1a.Id, 2, assignedToId: 4);
            await SeedStepAsync(db, proj1b.Id, init1b.Id, 1, assignedToId: 4);

            // Tenant 2: 1 initiative
            var init2a = await SeedInitiativeAsync(db, Tenant2Id, supervisorId: 6, code: "T2-INI-001");
            var proj2a = await SeedProjectAsync(db, init2a.Id, managerId: 7, code: "T2-PRJ-001");
            await SeedStepAsync(db, proj2a.Id, init2a.Id, 1, assignedToId: 7);
            await SeedStepAsync(db, proj2a.Id, init2a.Id, 2, assignedToId: 7);
        }
    }
}
