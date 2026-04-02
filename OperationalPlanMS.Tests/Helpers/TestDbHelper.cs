using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OperationalPlanMS.Services;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Tests.Helpers
{
    public static class TestDbHelper
    {
        public static AppDbContext CreateContext(string? dbName = null)
        {
            dbName ??= Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        public static IAuditService CreateAuditService() => new Mock<IAuditService>().Object;

        public static INotificationService CreateNotificationService() => new Mock<INotificationService>().Object;

        public static IUserService CreateUserService() => new Mock<IUserService>().Object;

        public static ILogger<T> CreateLogger<T>()
        {
            return new Mock<ILogger<T>>().Object;
        }

        public static async Task SeedBasicDataAsync(AppDbContext db)
        {
            if (!db.Roles.Any())
            {
                db.Roles.AddRange(
                    new Role { Id = 1, Code = "admin", NameAr = "???? ??????", NameEn = "Admin" },
                    new Role { Id = 2, Code = "executive", NameAr = "??????", NameEn = "Executive" },
                    new Role { Id = 3, Code = "supervisor", NameAr = "????", NameEn = "Supervisor" },
                    new Role { Id = 4, Code = "user", NameAr = "???? ?????", NameEn = "User" },
                    new Role { Id = 7, Code = "stepuser", NameAr = "???? ????", NameEn = "Step User" }
                );
                await db.SaveChangesAsync();
            }

            if (!db.Users.Any())
            {
                db.Users.AddRange(
                    new User { Id = 1, ADUsername = "C1-0001", FullNameAr = "???? ??????", FullNameEn = "Ahmed Supervisor", RoleId = 3, IsActive = true },
                    new User { Id = 2, ADUsername = "C1-0002", FullNameAr = "???? ??????", FullNameEn = "Salem Manager", RoleId = 4, IsActive = true },
                    new User { Id = 3, ADUsername = "C1-0003", FullNameAr = "???? ???????", FullNameEn = "Mohammed Admin", RoleId = 1, IsActive = true },
                    new User { Id = 4, ADUsername = "C1-0004", FullNameAr = "???? ??????", FullNameEn = "Yousef StepUser", RoleId = 7, IsActive = true },
                    new User { Id = 5, ADUsername = "C1-0005", FullNameAr = "???? ????", FullNameEn = "Khaled Inactive", RoleId = 4, IsActive = false }
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

        public static async Task<Initiative> SeedInitiativeAsync(AppDbContext db, int? supervisorId = 1, string code = "INI-2026-001")
        {
            var initiative = new Initiative
            {
                Code = code, NameAr = "?????? ???????", NameEn = "Test Initiative",
                FiscalYearId = 1, SupervisorId = supervisorId, CreatedById = 3, CreatedAt = DateTime.Now,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(6)
            };
            db.Initiatives.Add(initiative);
            await db.SaveChangesAsync();
            return initiative;
        }

        public static async Task<Project> SeedProjectAsync(AppDbContext db, int initiativeId, int? managerId = 2, string code = "PRJ-2026-001")
        {
            var project = new Project
            {
                Code = code, NameAr = "????? ??????", NameEn = "Test Project",
                InitiativeId = initiativeId, ProjectManagerId = managerId, CreatedById = 3, CreatedAt = DateTime.Now,
                Status = Status.InProgress, Priority = Priority.Medium,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddMonths(3)
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        }

        public static async Task<Step> SeedStepAsync(
            AppDbContext db, int projectId, int stepNumber = 1,
            decimal weight = 50, decimal progress = 0,
            int? assignedToId = 4, StepStatus status = StepStatus.NotStarted)
        {
            var step = new Step
            {
                StepNumber = stepNumber, NameAr = $"???? ??????? {stepNumber}", NameEn = $"Test Step {stepNumber}",
                ProjectId = projectId, Weight = weight, ProgressPercentage = progress, Status = status,
                AssignedToId = assignedToId, CreatedById = 3, CreatedAt = DateTime.Now,
                PlannedStartDate = DateTime.Today, PlannedEndDate = DateTime.Today.AddDays(30)
            };
            db.Steps.Add(step);
            await db.SaveChangesAsync();
            return step;
        }
    }
}
