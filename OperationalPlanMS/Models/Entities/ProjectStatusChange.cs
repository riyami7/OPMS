using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// سجل تغيير حالة المشروع — يُسجَّل عند كل تغيير حالة
    /// يتضمن السبب ونوع العائق والإجراء المتخذ
    /// </summary>
    [Table("ProjectStatusChanges")]
    public class ProjectStatusChange
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public Status OldStatus { get; set; }

        [Required]
        public Status NewStatus { get; set; }

        /// <summary>
        /// نوع العائق (إلزامي عند التوقف أو التأخر)
        /// </summary>
        public ObstacleType? ObstacleType { get; set; }

        /// <summary>
        /// وصف نوع العائق (عند اختيار "أخرى")
        /// </summary>
        [StringLength(200)]
        public string? ObstacleDescription { get; set; }

        /// <summary>
        /// السبب / التبرير (إلزامي عند OnHold, Delayed, Cancelled)
        /// </summary>
        [Required]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// الإجراء المتخذ أو المخطط له
        /// </summary>
        [StringLength(1000)]
        public string? ActionTaken { get; set; }

        /// <summary>
        /// التاريخ المتوقع للاستئناف (عند OnHold)
        /// </summary>
        [Column(TypeName = "date")]
        public DateTime? ExpectedResumeDate { get; set; }

        [Required]
        public int ChangedById { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.Now;

        // ========== Navigation Properties ==========

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("ChangedById")]
        public virtual User ChangedBy { get; set; } = null!;

        // ========== Computed Properties ==========

        [NotMapped]
        public bool IsNegativeChange =>
            NewStatus == Status.OnHold ||
            NewStatus == Status.Delayed ||
            NewStatus == Status.Cancelled;

        [NotMapped]
        public string ObstacleTypeDisplayAr => ObstacleType switch
        {
            Models.ObstacleType.Financial => "مالي",
            Models.ObstacleType.HumanResource => "نقص كوادر بشرية",
            Models.ObstacleType.Technical => "تقني",
            Models.ObstacleType.Administrative => "إداري",
            Models.ObstacleType.External => "خارجي",
            Models.ObstacleType.Procurement => "مشتريات / توريد",
            Models.ObstacleType.Other => !string.IsNullOrEmpty(ObstacleDescription) ? ObstacleDescription : "أخرى",
            _ => ""
        };

        [NotMapped]
        public string NewStatusDisplayAr => NewStatus switch
        {
            Status.Draft => "مسودة",
            Status.Pending => "معلّق",
            Status.Approved => "معتمد",
            Status.InProgress => "جاري التنفيذ",
            Status.OnHold => "متوقف مؤقتاً",
            Status.Completed => "مكتمل",
            Status.Cancelled => "ملغي",
            Status.Delayed => "متأخر",
            _ => ""
        };

        [NotMapped]
        public string StatusBadgeClass => NewStatus switch
        {
            Status.InProgress => "bg-primary",
            Status.Completed => "bg-success",
            Status.OnHold => "bg-warning text-dark",
            Status.Delayed => "bg-danger",
            Status.Cancelled => "bg-secondary",
            _ => "bg-info"
        };
    }
}
