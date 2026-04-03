using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Initiatives")]
    public class Initiative
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }

        [Required]
        public Status Status { get; set; } = Status.InProgress;

        [Required]
        public Priority Priority { get; set; } = Priority.Medium;

        [Required]
        [Column(TypeName = "date")]
        public DateTime PlannedStartDate { get; set; } = DateTime.Today;

        [Required]
        [Column(TypeName = "date")]
        public DateTime PlannedEndDate { get; set; } = DateTime.Today.AddMonths(6);

        [Column(TypeName = "decimal(5,2)")]
        public decimal ProgressPercentage { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } = 10;

        [Column(TypeName = "date")]
        public DateTime? ActualStartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? ActualEndDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Budget { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        [StringLength(500)]
        public string? StrategicObjective { get; set; }

        // ========== الوحدة التنظيمية من API ==========
        public Guid? ExternalUnitId { get; set; }

        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        // ========== Multi-Tenancy ==========
        /// <summary>
        /// معرف الـ Tenant — الوحدة التنظيمية المستوى الأول (الجذر)
        /// يُحسب تلقائياً من ExternalUnitId عند الإنشاء
        /// </summary>
        public Guid? TenantId { get; set; }

        // ========== المشرف من API ==========
        [StringLength(50)]
        public string? SupervisorEmpNumber { get; set; }

        [StringLength(200)]
        public string? SupervisorName { get; set; }

        [StringLength(100)]
        public string? SupervisorRank { get; set; }

        [Required]
        public int FiscalYearId { get; set; }

        public int? SupervisorId { get; set; }

        [Required]
        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        [ForeignKey("FiscalYearId")]
        public virtual FiscalYear FiscalYear { get; set; } = null!;

        [ForeignKey("SupervisorId")]
        public virtual User? Supervisor { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
        public virtual ICollection<ProgressUpdate> ProgressUpdates { get; set; } = new List<ProgressUpdate>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<InitiativeAccess> AccessList { get; set; } = new List<InitiativeAccess>();

        [NotMapped]
        public string SupervisorDisplayName => !string.IsNullOrEmpty(SupervisorName)
            ? $"{SupervisorRank} {SupervisorName}".Trim()
            : Supervisor?.FullNameAr ?? "";

        [NotMapped]
        public string UnitDisplayName => ExternalUnitName ?? ExternalUnit?.DisplayName ?? "";
    }
}
