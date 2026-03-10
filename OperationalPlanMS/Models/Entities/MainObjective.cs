using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// الأهداف الرئيسية (مرتبطة بالأهداف الاستراتيجية)
    /// </summary>
    [Table("MainObjectives")]
    public class MainObjective
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
        [StringLength(500)]
        public string NameAr { get; set; } = string.Empty;

        /// <summary>
        /// الاسم بالإنجليزية
        /// </summary>
        [StringLength(500)]
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
        /// الهدف الاستراتيجي
        /// </summary>
        [Required]
        public int StrategicObjectiveId { get; set; }

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
        [ForeignKey("StrategicObjectiveId")]
        public virtual StrategicObjective StrategicObjective { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        /// <summary>
        /// الأهداف الفرعية المرتبطة بهذا الهدف
        /// </summary>
        public virtual ICollection<SubObjective> SubObjectives { get; set; } = new List<SubObjective>();
    }
}
