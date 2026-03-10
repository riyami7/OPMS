using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// نسب السنوات للمشاريع متعددة السنوات
    /// </summary>
    [Table("ProjectYearTargets")]
    public class ProjectYearTarget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        /// <summary>
        /// السنة (مثل 2024, 2025, 2026)
        /// </summary>
        [Required]
        public int Year { get; set; }

        /// <summary>
        /// النسبة المستهدفة لهذه السنة (من 0 إلى 100)
        /// مجموع كل السنوات يجب أن يساوي 100%
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal TargetPercentage { get; set; } = 0;

        /// <summary>
        /// ملاحظات
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        // ========== Computed Properties ==========
        
        /// <summary>
        /// النسبة الفعلية المنجزة لهذه السنة
        /// تُحسب من الخطوات التي تاريخ نهايتها في هذه السنة
        /// </summary>
        [NotMapped]
        public decimal ActualPercentage { get; set; } = 0;

        /// <summary>
        /// نسبة إنجاز السنة (الفعلي / المستهدف * 100)
        /// </summary>
        [NotMapped]
        public decimal YearCompletionPercentage
        {
            get
            {
                if (TargetPercentage == 0) return 0;
                var completion = (ActualPercentage / TargetPercentage) * 100;
                return Math.Min(Math.Round(completion, 2), 100);
            }
        }

        /// <summary>
        /// هل تم إنجاز هدف السنة
        /// </summary>
        [NotMapped]
        public bool IsYearCompleted => ActualPercentage >= TargetPercentage;
    }
}
