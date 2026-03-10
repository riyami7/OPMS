using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// القيم المؤسسية
    /// </summary>
    [Table("CoreValues")]
    public class CoreValue
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// اسم القيمة بالعربية
        /// </summary>
        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        /// <summary>
        /// اسم القيمة بالإنجليزية
        /// </summary>
        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// معنى القيمة بالعربية
        /// </summary>
        public string? MeaningAr { get; set; }

        /// <summary>
        /// معنى القيمة بالإنجليزية
        /// </summary>
        public string? MeaningEn { get; set; }

        /// <summary>
        /// أيقونة القيمة (Bootstrap Icons)
        /// </summary>
        [StringLength(100)]
        public string? Icon { get; set; }

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
    }
}
