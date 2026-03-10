using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// الأهداف الفرعية (مرتبطة بالأهداف الرئيسية والوحدات التنظيمية)
    /// </summary>
    [Table("SubObjectives")]
    public class SubObjective
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
        /// الهدف الرئيسي
        /// </summary>
        [Required]
        public int MainObjectiveId { get; set; }

        // ========== الوحدة التنظيمية المحلية (للتوافق) ==========
        /// <summary>
        /// الوحدة التنظيمية المحلية (اختياري - للتوافق مع البيانات القديمة)
        /// </summary>
        public int? OrganizationalUnitId { get; set; }

        // ========== الهيكل التنظيمي من API الخارجي (جديد) ==========

        /// <summary>
        /// معرف الوحدة التنظيمية من API الخارجي
        /// </summary>
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة المختارة من API (للعرض السريع)
        /// </summary>
        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        // ==========================================================

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
        [ForeignKey("MainObjectiveId")]
        public virtual MainObjective MainObjective { get; set; } = null!;

        [ForeignKey("OrganizationalUnitId")]
        public virtual OrganizationalUnit? OrganizationalUnit { get; set; }

        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        // ========== Computed Properties ==========

        /// <summary>
        /// اسم الوحدة التنظيمية للعرض
        /// </summary>
        [NotMapped]
        public string UnitDisplayName => !string.IsNullOrEmpty(ExternalUnitName)
            ? ExternalUnitName
            : OrganizationalUnit?.NameAr ?? "";
    }
}