using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// مؤشرات الأداء للمشروع
    /// </summary>
    [Table("ProjectKPIs")]
    public class ProjectKPI
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(500)]
        public string KPIText { get; set; } = string.Empty;

        /// <summary>
        /// القيمة المستهدفة
        /// </summary>
        [StringLength(100)]
        public string? TargetValue { get; set; }

        /// <summary>
        /// القيمة الفعلية
        /// </summary>
        [StringLength(100)]
        public string? ActualValue { get; set; }

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;
    }
}
