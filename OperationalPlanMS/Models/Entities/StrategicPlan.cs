using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// الخطة الاستراتيجية الخمسية
    /// </summary>
    [Table("StrategicPlans")]
    public class StrategicPlan
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الاسم بالعربية — مثال: "الخطة الخمسية 2026-2030"
        /// </summary>
        [Required]
        [StringLength(300)]
        public string NameAr { get; set; } = string.Empty;

        /// <summary>
        /// الاسم بالإنجليزية
        /// </summary>
        [StringLength(300)]
        public string? NameEn { get; set; }

        /// <summary>
        /// الوصف
        /// </summary>
        public string? DescriptionAr { get; set; }

        /// <summary>
        /// سنة البداية — مثال: 2026
        /// </summary>
        [Required]
        public int StartYear { get; set; }

        /// <summary>
        /// سنة النهاية — مثال: 2030
        /// </summary>
        [Required]
        public int EndYear { get; set; }

        /// <summary>
        /// هل هي الخطة الحالية؟
        /// </summary>
        public bool IsCurrent { get; set; } = false;

        /// <summary>
        /// نشطة
        /// </summary>
        public bool IsActive { get; set; } = true;

        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        // Navigation
        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        /// <summary>
        /// المحاور المرتبطة بهذه الخطة
        /// </summary>
        public virtual ICollection<StrategicAxis> Axes { get; set; } = new List<StrategicAxis>();

        // === Computed ===

        /// <summary>
        /// مدة الخطة بالسنوات
        /// </summary>
        [NotMapped]
        public int DurationYears => EndYear - StartYear + 1;

        /// <summary>
        /// اسم العرض — "2026 - 2030"
        /// </summary>
        [NotMapped]
        public string DisplayName => $"{StartYear} - {EndYear}";
    }
}
