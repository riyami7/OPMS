using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// إعدادات الوحدة التنظيمية (الرؤية والمهمة لكل وحدة)
    /// </summary>
    [Table("OrganizationalUnitSettings")]
    public class OrganizationalUnitSettings
    {
        [Key]
        public int Id { get; set; }

        // ========== الوحدة التنظيمية المحلية (للتوافق) ==========
        /// <summary>
        /// الوحدة التنظيمية المحلية (اختياري - للتوافق)
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
        /// الرؤية بالعربية
        /// </summary>
        [StringLength(1000)]
        public string? VisionAr { get; set; }

        /// <summary>
        /// الرؤية بالإنجليزية
        /// </summary>
        [StringLength(1000)]
        public string? VisionEn { get; set; }

        /// <summary>
        /// المهمة بالعربية
        /// </summary>
        [StringLength(1000)]
        public string? MissionAr { get; set; }

        /// <summary>
        /// المهمة بالإنجليزية
        /// </summary>
        [StringLength(1000)]
        public string? MissionEn { get; set; }

        /// <summary>
        /// وصف إضافي بالعربية
        /// </summary>
        public string? DescriptionAr { get; set; }

        /// <summary>
        /// وصف إضافي بالإنجليزية
        /// </summary>
        public string? DescriptionEn { get; set; }

        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        // Navigation
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