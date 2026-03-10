using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// ربط المشروع بالجهات المساندة مع ممثل كل جهة
    /// يدعم الجهات المحلية (SupportingEntities) والجهات من API (ExternalOrganizationalUnits)
    /// </summary>
    [Table("ProjectSupportingUnits")]
    public class ProjectSupportingUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        // ========== الجهة المساندة المحلية (للتوافق مع النظام القديم) ==========

        /// <summary>
        /// معرف الجهة المساندة من الجدول المحلي (اختياري)
        /// </summary>
        public int? SupportingEntityId { get; set; }

        // ========== الجهة المساندة من API ==========

        /// <summary>
        /// معرف الوحدة من ExternalOrganizationalUnits
        /// </summary>
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة (للحفظ والعرض السريع)
        /// </summary>
        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        // ========== ممثل الجهة ==========

        /// <summary>
        /// رقم ممثل الجهة (من API الموظفين)
        /// </summary>
        [StringLength(50)]
        public string? RepresentativeEmpNumber { get; set; }

        /// <summary>
        /// اسم ممثل الجهة
        /// </summary>
        [StringLength(200)]
        public string? RepresentativeName { get; set; }

        /// <summary>
        /// رتبة ممثل الجهة
        /// </summary>
        [StringLength(100)]
        public string? RepresentativeRank { get; set; }

        // ========== Audit ==========
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ========== Navigation ==========

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("SupportingEntityId")]
        public virtual SupportingEntity? SupportingEntity { get; set; }

        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        // ========== Helper Properties ==========

        /// <summary>
        /// اسم الجهة للعرض (من API أو من الجدول المحلي)
        /// </summary>
        [NotMapped]
        public string DisplayName => !string.IsNullOrEmpty(ExternalUnitName)
            ? ExternalUnitName
            : SupportingEntity?.NameAr ?? "";

        /// <summary>
        /// الاسم الكامل للممثل
        /// </summary>
        [NotMapped]
        public string RepresentativeFullName => !string.IsNullOrEmpty(RepresentativeName)
            ? $"{RepresentativeRank} {RepresentativeName}".Trim()
            : "";

        /// <summary>
        /// هل الجهة من API الخارجي
        /// </summary>
        [NotMapped]
        public bool IsExternalUnit => ExternalUnitId.HasValue;
    }
}