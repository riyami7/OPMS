using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// رقم الموظف من نظام HR (مثل C1-1234)
        /// يُستخدم أيضاً كـ AD Username للمصادقة
        /// </summary>
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

        public int? OrganizationalUnitId { get; set; }

        [Required]
        public int OrganizationId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsStepApprover { get; set; } = false;

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }

        #region Employee API Fields - حقول من نظام الموارد البشرية

        /// <summary>
        /// الرتبة من API الموظفين
        /// </summary>
        [StringLength(100)]
        public string? EmployeeRank { get; set; }

        /// <summary>
        /// المنصب/الوظيفة من API الموظفين
        /// </summary>
        [StringLength(200)]
        public string? EmployeePosition { get; set; }

        /// <summary>
        /// اسم الفرع من API الموظفين
        /// </summary>
        [StringLength(200)]
        public string? BranchName { get; set; }

        /// <summary>
        /// معرف الوحدة التنظيمية من API الخارجي
        /// </summary>
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة التنظيمية من API الخارجي
        /// </summary>
        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        #endregion

        #region Navigation Properties

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        [ForeignKey("OrganizationalUnitId")]
        public virtual OrganizationalUnit? OrganizationalUnit { get; set; }

        [ForeignKey("OrganizationId")]
        public virtual Organization Organization { get; set; } = null!;

        // Initiatives where this user is supervisor
        [InverseProperty("Supervisor")]
        public virtual ICollection<Initiative> SupervisedInitiatives { get; set; } = new List<Initiative>();

        // Initiatives created by this user
        [InverseProperty("CreatedBy")]
        public virtual ICollection<Initiative> CreatedInitiatives { get; set; } = new List<Initiative>();

        // Projects managed by this user
        [InverseProperty("ProjectManager")]
        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();

        // Steps assigned to this user
        [InverseProperty("AssignedTo")]
        public virtual ICollection<Step> AssignedSteps { get; set; } = new List<Step>();

        #endregion

        #region Computed Properties

        /// <summary>
        /// اسم العرض الكامل مع الرتبة
        /// </summary>
        [NotMapped]
        public string DisplayNameWithRank => !string.IsNullOrEmpty(EmployeeRank)
            ? $"{EmployeeRank} {FullNameAr}".Trim()
            : FullNameAr;

        /// <summary>
        /// اسم الوحدة للعرض (API أو محلي)
        /// </summary>
        [NotMapped]
        public string UnitDisplayName => !string.IsNullOrEmpty(ExternalUnitName)
            ? ExternalUnitName
            : OrganizationalUnit?.NameAr ?? "-";

        #endregion
    }
}
