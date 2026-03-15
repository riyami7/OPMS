using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<Initiative> Initiatives { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Step> Steps { get; set; }
        public DbSet<Milestone> Milestones { get; set; }
        public DbSet<ProgressUpdate> ProgressUpdates { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<ProjectRequirement> ProjectRequirements { get; set; }
        public DbSet<ProjectKPI> ProjectKPIs { get; set; }
        public DbSet<ProjectSupportingUnit> ProjectSupportingUnits { get; set; }
        public DbSet<ProjectYearTarget> ProjectYearTargets { get; set; }
        public DbSet<SupportingEntity> SupportingEntities { get; set; }
        public DbSet<StepAttachment> StepAttachments { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<OrganizationalUnitSettings> OrganizationalUnitSettings { get; set; }
        public DbSet<StrategicAxis> StrategicAxes { get; set; }
        public DbSet<StrategicObjective> StrategicObjectives { get; set; }
        public DbSet<MainObjective> MainObjectives { get; set; }
        public DbSet<SubObjective> SubObjectives { get; set; }
        public DbSet<CoreValue> CoreValues { get; set; }
        public DbSet<FinancialCost> FinancialCosts { get; set; }
        public DbSet<ExternalOrganizationalUnit> ExternalOrganizationalUnits { get; set; }
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<InitiativeAccess> InitiativeAccess { get; set; }
        public DbSet<ProjectSubObjective> ProjectSubObjectives { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
            });

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.ADUsername).IsUnique();

                entity.HasOne(e => e.Role)
                    .WithMany(e => e.Users)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ExternalUnit)
                    .WithMany()
                    .HasForeignKey(e => e.ExternalUnitId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // FiscalYear - لا يوجد OrganizationId بعد الآن
            // (لا تحتاج تهيئة خاصة)

            // Initiative
            modelBuilder.Entity<Initiative>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.ExternalUnit)
                    .WithMany(e => e.Initiatives)
                    .HasForeignKey(e => e.ExternalUnitId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.FiscalYear)
                    .WithMany(e => e.Initiatives)
                    .HasForeignKey(e => e.FiscalYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Supervisor)
                    .WithMany(e => e.SupervisedInitiatives)
                    .HasForeignKey(e => e.SupervisorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany(e => e.CreatedInitiatives)
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Project
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.Initiative)
                    .WithMany(e => e.Projects)
                    .HasForeignKey(e => e.InitiativeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ExternalUnit)
                    .WithMany(e => e.Projects)
                    .HasForeignKey(e => e.ExternalUnitId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ProjectManager)
                    .WithMany(e => e.ManagedProjects)
                    .HasForeignKey(e => e.ProjectManagerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.SubObjective)
                    .WithMany()
                    .HasForeignKey(e => e.SubObjectiveId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.FinancialCost)
                    .WithMany()
                    .HasForeignKey(e => e.FinancialCostId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Step
            modelBuilder.Entity<Step>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.Steps)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedTo)
                    .WithMany(e => e.AssignedSteps)
                    .HasForeignKey(e => e.AssignedToId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DependsOnStep)
                    .WithMany(e => e.DependentSteps)
                    .HasForeignKey(e => e.DependsOnStepId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Milestone
            modelBuilder.Entity<Milestone>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.Milestones)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ProgressUpdate
            modelBuilder.Entity<ProgressUpdate>(entity =>
            {
                entity.HasOne(e => e.Initiative)
                    .WithMany(e => e.ProgressUpdates)
                    .HasForeignKey(e => e.InitiativeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Project)
                    .WithMany(e => e.ProgressUpdates)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Step)
                    .WithMany(e => e.ProgressUpdates)
                    .HasForeignKey(e => e.StepId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Document
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasOne(e => e.PreviousVersion)
                    .WithMany(e => e.NewerVersions)
                    .HasForeignKey(e => e.PreviousVersionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Initiative)
                    .WithMany(e => e.Documents)
                    .HasForeignKey(e => e.InitiativeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Project)
                    .WithMany(e => e.Documents)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ProjectRequirement
            modelBuilder.Entity<ProjectRequirement>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.Requirements)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ProjectKPI
            modelBuilder.Entity<ProjectKPI>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.ProjectKPIs)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ProjectSupportingUnit
            modelBuilder.Entity<ProjectSupportingUnit>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.SupportingUnits)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SupportingEntity)
                    .WithMany(s => s.ProjectSupportingUnits)
                    .HasForeignKey(e => e.SupportingEntityId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ExternalUnit)
                    .WithMany()
                    .HasForeignKey(e => e.ExternalUnitId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ProjectYearTarget
            modelBuilder.Entity<ProjectYearTarget>(entity =>
            {
                entity.HasOne(e => e.Project)
                    .WithMany(e => e.YearTargets)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ProjectId, e.Year }).IsUnique();
            });

            // SupportingEntity
            modelBuilder.Entity<SupportingEntity>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            // ExternalOrganizationalUnit
            modelBuilder.Entity<ExternalOrganizationalUnit>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // StrategicAxis
            modelBuilder.Entity<StrategicAxis>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // StrategicObjective
            modelBuilder.Entity<StrategicObjective>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.StrategicAxis)
                    .WithMany(e => e.StrategicObjectives)
                    .HasForeignKey(e => e.StrategicAxisId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // MainObjective
            modelBuilder.Entity<MainObjective>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.StrategicObjective)
                    .WithMany(e => e.MainObjectives)
                    .HasForeignKey(e => e.StrategicObjectiveId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SubObjective
            modelBuilder.Entity<SubObjective>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();

                entity.HasOne(e => e.MainObjective)
                    .WithMany(e => e.SubObjectives)
                    .HasForeignKey(e => e.MainObjectiveId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ExternalUnit)
                    .WithMany()
                    .HasForeignKey(e => e.ExternalUnitId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CoreValue
            modelBuilder.Entity<CoreValue>(entity =>
            {
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // FinancialCost
            modelBuilder.Entity<FinancialCost>(entity =>
            {
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.LastModifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.LastModifiedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // InitiativeAccess
            modelBuilder.Entity<InitiativeAccess>(entity =>
            {
                // One user can't have duplicate access to the same initiative
                entity.HasIndex(e => new { e.UserId, e.InitiativeId }).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.InitiativeAccessList)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Initiative)
                    .WithMany(i => i.AccessList)
                    .HasForeignKey(e => e.InitiativeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.GrantedBy)
                    .WithMany()
                    .HasForeignKey(e => e.GrantedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ProjectSubObjective (many-to-many)
            modelBuilder.Entity<ProjectSubObjective>(entity =>
            {
                entity.HasIndex(e => new { e.ProjectId, e.SubObjectiveId }).IsUnique();

                entity.HasOne(e => e.Project)
                    .WithMany(p => p.ProjectSubObjectives)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SubObjective)
                    .WithMany()
                    .HasForeignKey(e => e.SubObjectiveId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
