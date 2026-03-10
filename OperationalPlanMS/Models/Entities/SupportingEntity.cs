using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// جهات المساندة - جدول مستقل يديره Admin
    /// </summary>
    [Table("SupportingEntities")]
    public class SupportingEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        
        [StringLength(200)]
        public string? NameEn { get; set; } = string.Empty;

        /// <summary>
        /// المنظمة التابعة لها
        /// </summary>
        [Required]
        public int OrganizationId { get; set; }

        public int OrderIndex { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        // Audit
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? LastModifiedById { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        // Navigation
        [ForeignKey("OrganizationId")]
        public virtual Organization Organization { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }

        public virtual ICollection<ProjectSupportingUnit> ProjectSupportingUnits { get; set; } = new List<ProjectSupportingUnit>();
    }
}
