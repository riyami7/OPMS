using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Projects")]
    public class Project
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الكود (يُولد تلقائياً)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// رقم المشروع (يدخله المستخدم)
        /// </summary>
        [StringLength(50)]
        public string? ProjectNumber { get; set; }

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public string? DescriptionEn { get; set; }

        /// <summary>
        /// الهدف التشغيلي
        /// </summary>
        public string? OperationalGoal { get; set; }

        // ========== الحقول المُلغاة (تبقى للتوافق مع DB) ==========
        [Required]
        public Status Status { get; set; } = Status.InProgress;

        [Required]
        public Priority Priority { get; set; } = Priority.Medium;

        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } = 10;
        // ==========================================================

        // ========== التواريخ ==========
        [Column(TypeName = "date")]
        public DateTime? PlannedStartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? PlannedEndDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? ActualStartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? ActualEndDate { get; set; }

        // ========== نسبة الإنجاز ==========
        [Column(TypeName = "decimal(5,2)")]
        public decimal ProgressPercentage { get; set; } = 0;

        // ========== الميزانية ==========
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Budget { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        // ========== معلومات إضافية ==========
        public string? ExpectedOutcomes { get; set; }

        public string? KPIs { get; set; }

        public string? RiskNotes { get; set; }

        // ========== الهيكل التنظيمي من API الخارجي ==========

        /// <summary>
        /// معرف الوحدة التنظيمية من API الخارجي
        /// يحفظ آخر مستوى مختار (1 أو 2 أو 3)
        /// </summary>
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة المختارة من API (للعرض السريع)
        /// </summary>
        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        // ========== مدير المشروع من API الخارجي (جديد) ==========

        /// <summary>
        /// رقم مدير المشروع (من API الخارجي)
        /// </summary>
        [StringLength(50)]
        public string? ProjectManagerEmpNumber { get; set; }

        /// <summary>
        /// اسم مدير المشروع (من API)
        /// </summary>
        [StringLength(200)]
        public string? ProjectManagerName { get; set; }

        /// <summary>
        /// رتبة مدير المشروع (من API)
        /// </summary>
        [StringLength(100)]
        public string? ProjectManagerRank { get; set; }

        // ========== الهدف الفرعي (جديد) ==========

        /// <summary>
        /// الهدف الفرعي
        /// </summary>
        public int? SubObjectiveId { get; set; }

        // ========== التكلفة المالية (جديد) ==========

        /// <summary>
        /// نوع التكلفة المالية
        /// </summary>
        public int? FinancialCostId { get; set; }

        // ========== العلاقات ==========
        [Required]
        public int InitiativeId { get; set; }

        /// <summary>
        /// الوحدة التنظيمية المحلية (اختياري - للتوافق مع البيانات القديمة)
        /// </summary>
        public int? OrganizationalUnitId { get; set; }

        public int? ProjectManagerId { get; set; }

        [Required]
        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? LastModifiedById { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        // ========== Navigation properties ==========
        [ForeignKey("InitiativeId")]
        public virtual Initiative Initiative { get; set; } = null!;

        [ForeignKey("OrganizationalUnitId")]
        public virtual OrganizationalUnit? OrganizationalUnit { get; set; }

        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        [ForeignKey("ProjectManagerId")]
        public virtual User? ProjectManager { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        // Navigation للحقول الجديدة
        [ForeignKey("SubObjectiveId")]
        public virtual SubObjective? SubObjective { get; set; }

        [ForeignKey("FinancialCostId")]
        public virtual FinancialCost? FinancialCost { get; set; }

        // ========== Collections ==========
        public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
        public virtual ICollection<Step> Steps { get; set; } = new List<Step>();
        public virtual ICollection<ProgressUpdate> ProgressUpdates { get; set; } = new List<ProgressUpdate>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

        // ========== الجداول الجديدة ==========
        public virtual ICollection<ProjectRequirement> Requirements { get; set; } = new List<ProjectRequirement>();
        public virtual ICollection<ProjectKPI> ProjectKPIs { get; set; } = new List<ProjectKPI>();
        public virtual ICollection<ProjectSupportingUnit> SupportingUnits { get; set; } = new List<ProjectSupportingUnit>();
        public virtual ICollection<ProjectYearTarget> YearTargets { get; set; } = new List<ProjectYearTarget>();

        // ========== Computed Properties ==========
        
        [NotMapped]
        public decimal CalculatedProgress
        {
            get
            {
                if (Steps == null || !Steps.Any()) return 0;
                var activeSteps = Steps.Where(s => !s.IsDeleted).ToList();
                if (!activeSteps.Any()) return 0;

                return activeSteps
                    .Where(s => s.ProgressPercentage >= 100)
                    .Sum(s => s.Weight);
            }
        }

        [NotMapped]
        public bool IsCompleted => ProgressPercentage >= 100;

        [NotMapped]
        public int ProjectDurationYears
        {
            get
            {
                if (!PlannedStartDate.HasValue || !PlannedEndDate.HasValue) return 1;
                return PlannedEndDate.Value.Year - PlannedStartDate.Value.Year + 1;
            }
        }

        [NotMapped]
        public bool IsMultiYear => ProjectDurationYears > 1;

        [NotMapped]
        public List<int> ProjectYears
        {
            get
            {
                if (!PlannedStartDate.HasValue || !PlannedEndDate.HasValue)
                    return new List<int> { DateTime.Now.Year };

                var years = new List<int>();
                for (int y = PlannedStartDate.Value.Year; y <= PlannedEndDate.Value.Year; y++)
                {
                    years.Add(y);
                }
                return years;
            }
        }
    }
}
