using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// المحاور الرئيسية (الاستراتيجية)
    /// </summary>
    [Table("StrategicAxes")]
    public class StrategicAxis
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الكود (تلقائي)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// الاسم بالعربية
        /// </summary>
        [Required]
        [StringLength(300)]
        public string NameAr { get; set; } = string.Empty;

        /// <summary>
        /// الاسم بالإنجليزية
        /// </summary>
        [StringLength(300)]
        public string? NameEn { get; set; } = string.Empty;

        /// <summary>
        /// الوصف بالعربية
        /// </summary>
        public string? DescriptionAr { get; set; }

        /// <summary>
        /// الوصف بالإنجليزية
        /// </summary>
        public string? DescriptionEn { get; set; }

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        /// <summary>
        /// نشط
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
        /// الأهداف الاستراتيجية المرتبطة بهذا المحور
        /// </summary>
        public virtual ICollection<StrategicObjective> StrategicObjectives { get; set; } = new List<StrategicObjective>();
    }
}
