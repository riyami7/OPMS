using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// إعدادات الوحدة التنظيمية (الرؤية والمهمة لكل وحدة من API)
    /// </summary>
    [Table("OrganizationalUnitSettings")]
    public class OrganizationalUnitSettings
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// معرف الوحدة التنظيمية من API الخارجي
        /// </summary>
        public int? ExternalUnitId { get; set; }

        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        [StringLength(1000)]
        public string? VisionAr { get; set; }

        [StringLength(1000)]
        public string? VisionEn { get; set; }

        [StringLength(1000)]
        public string? MissionAr { get; set; }

        [StringLength(1000)]
        public string? MissionEn { get; set; }

        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }

        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        [ForeignKey("ExternalUnitId")]
        public virtual ExternalOrganizationalUnit? ExternalUnit { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        [NotMapped]
        public string UnitDisplayName => ExternalUnitName ?? ExternalUnit?.DisplayName ?? "";
    }
}
