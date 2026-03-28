using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("SubObjectives")]
    public class SubObjective
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(500)]
        public string? NameEn { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }

        [Required]
        public int MainObjectiveId { get; set; }

        // ========== الوحدة التنظيمية من API ==========
        public Guid? ExternalUnitId { get; set; }

        [StringLength(300)]
        public string? ExternalUnitName { get; set; }

        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        // Navigation
        [ForeignKey("MainObjectiveId")]
        public virtual MainObjective MainObjective { get; set; } = null!;

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
