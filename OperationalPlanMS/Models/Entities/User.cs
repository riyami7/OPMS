using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ADUsername { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Email { get; set; }

        [Required]
        [StringLength(200)]
        public string FullNameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string FullNameEn { get; set; } = string.Empty;

        [StringLength(500)]
        public string? PasswordHash { get; set; }

        [StringLength(500)]
        public string? ProfileImage { get; set; }

        [Required]
        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsStepApprover { get; set; } = false;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedBy { get; set; }

        #region Multi-Tenancy

        /// <summary>
        /// معرف الـ Tenant — الوحدة التنظيمية المستوى الأول (الجذر)
        /// null = SuperAdmin (يشوف كل الـ tenants)
        /// </summary>
        public Guid? TenantId { get; set; }

        #endregion

        #region Employee API Fields

        [StringLength(100)]
        public string? EmployeeRank { get; set; }

        [StringLength(200)]
        public string? EmployeePosition { get; set; }

        [StringLength(200)]
        public string? BranchName { get; set; }

        /// <summary>
        /// معرف الوحدة التنظيمية من API الخارجي
        /// </summary>
        public Guid? ExternalUnitId { get; set; }

        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        #endregion

        #region Navigation Properties

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        [InverseProperty("Supervisor")]
        public virtual ICollection<Initiative> SupervisedInitiatives { get; set; } = new List<Initiative>();

        [InverseProperty("CreatedBy")]
        public virtual ICollection<Initiative> CreatedInitiatives { get; set; } = new List<Initiative>();

        [InverseProperty("ProjectManager")]
        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();

        [InverseProperty("AssignedTo")]
        public virtual ICollection<Step> AssignedSteps { get; set; } = new List<Step>();

        [InverseProperty("User")]
        public virtual ICollection<InitiativeAccess> InitiativeAccessList { get; set; } = new List<InitiativeAccess>();

        #endregion

        #region Computed Properties

        [NotMapped]
        public string DisplayNameWithRank => !string.IsNullOrEmpty(EmployeeRank)
            ? $"{EmployeeRank} {FullNameAr}".Trim()
            : FullNameAr;

        [NotMapped]
        public string UnitDisplayName => ExternalUnitName
            ?? ExternalUnit?.DisplayName
            ?? "-";

        #endregion
    }
}
