using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Steps")]
    public class Step
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StepNumber { get; set; }

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public string? DescriptionEn { get; set; }

        [Required]
        public StepStatus Status { get; set; } = StepStatus.NotStarted;

        // ========== الحقول المُلغاة (تبقى للتوافق مع DB) ==========
        [Required]
        [Column(TypeName = "date")]
        public DateTime PlannedStartDate { get; set; } = DateTime.Today;

        [Required]
        [Column(TypeName = "date")]
        public DateTime PlannedEndDate { get; set; } = DateTime.Today.AddDays(7);
        // ==========================================================

        // ========== الحقول المستخدمة ==========
        [Column(TypeName = "date")]
        public DateTime? ActualStartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? ActualEndDate { get; set; }

        /// <summary>
        /// نسبة إنجاز الخطوة (0-100)
        /// عند الوصول لـ 100% ينعكس الوزن على المشروع
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal ProgressPercentage { get; set; } = 0;

        /// <summary>
        /// وزن الخطوة من إجمالي المشروع (مجموع أوزان الخطوات = 100)
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } = 10;

        public string? Notes { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public int? InitiativeId { get; set; }

        // ========== مسؤول الخطوة (النظام القديم - للتوافق) ==========
        public int? AssignedToId { get; set; }

        // ========== مسؤول الخطوة من API (جديد) ==========

        /// <summary>
        /// رقم الموظف المسؤول (من API)
        /// </summary>
        [StringLength(50)]
        public string? AssignedToEmpNumber { get; set; }

        /// <summary>
        /// اسم الموظف المسؤول (من API)
        /// </summary>
        [StringLength(200)]
        public string? AssignedToName { get; set; }

        /// <summary>
        /// رتبة الموظف المسؤول (من API)
        /// </summary>
        [StringLength(100)]
        public string? AssignedToRank { get; set; }

        // ==========================================================

        public int? DependsOnStepId { get; set; }

        [Required]
        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? LastModifiedById { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        public bool IsDeleted { get; set; } = false;


        /// <summary>
        /// حالة التأكيد
        /// </summary>
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.None;

        /// <summary>
        /// سبب الرفض (في حالة الرفض)
        /// </summary>
        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        /// <summary>
        /// تفاصيل الإتمام (يكتبها مسؤول الخطوة عند الإكمال)
        /// </summary>
        public string? CompletionDetails { get; set; }

        /// <summary>
        /// المؤكد
        /// </summary>
        public int? ApprovedById { get; set; }

        /// <summary>
        /// تاريخ التأكيد
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// تاريخ الإرسال للتأكيد
        /// </summary>
        public DateTime? SubmittedForApprovalAt { get; set; }

        // Navigation properties
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("AssignedToId")]
        public virtual User? AssignedTo { get; set; }

        [ForeignKey("DependsOnStepId")]
        public virtual Step? DependsOnStep { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        [ForeignKey("ApprovedById")]
        public virtual User? ApprovedBy { get; set; }

        public virtual ICollection<Step> DependentSteps { get; set; } = new List<Step>();
        public virtual ICollection<ProgressUpdate> ProgressUpdates { get; set; } = new List<ProgressUpdate>();
        public virtual ICollection<StepAttachment> Attachments { get; set; } = new List<StepAttachment>();

        // ========== Computed Properties ==========

        /// <summary>
        /// اسم المسؤول للعرض (من API أو من النظام القديم)
        /// </summary>
        [NotMapped]
        public string AssignedToDisplayName => !string.IsNullOrEmpty(AssignedToName)
            ? $"{AssignedToRank} {AssignedToName}".Trim()
            : AssignedTo?.FullNameAr ?? "";

        /// <summary>
        /// هل الخطوة متأخرة؟
        /// تكون متأخرة إذا تجاوز تاريخ النهاية الفعلي ولم تكتمل 100%
        /// </summary>
        [NotMapped]
        public bool IsDelayed =>
            ActualEndDate.HasValue &&
            ActualEndDate.Value < DateTime.Today &&
            ProgressPercentage < 100;

        /// <summary>
        /// هل الخطوة مكتملة؟
        /// </summary>
        [NotMapped]
        public bool IsCompleted => ProgressPercentage >= 100;

        /// <summary>
        /// الحالة الفعلية (تُحسب تلقائياً)
        /// </summary>
        [NotMapped]
        public StepStatus CalculatedStatus
        {
            get
            {
                if (Status == StepStatus.Cancelled) return StepStatus.Cancelled;
                if (Status == StepStatus.OnHold) return StepStatus.OnHold;
                if (ProgressPercentage >= 100) return StepStatus.Completed;
                if (IsDelayed) return StepStatus.Delayed;
                if (ProgressPercentage > 0) return StepStatus.InProgress;
                return StepStatus.NotStarted;
            }
        }
    }
}