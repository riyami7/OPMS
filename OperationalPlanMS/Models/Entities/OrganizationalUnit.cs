using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("OrganizationalUnits")]
    public class OrganizationalUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        public int? ParentUnitId { get; set; }

        [Required]
        public int OrganizationId { get; set; }

        public int UnitLevel { get; set; } = 1;  // مستوى الوحدة

        public int SortOrder { get; set; } = 0;  // ترتيب العرض

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// هل يمكن اختيار هذه الوحدة كجهة مساندة للمشاريع
        /// </summary>

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }

        // Navigation properties
        [ForeignKey("ParentUnitId")]
        public virtual OrganizationalUnit? ParentUnit { get; set; }

        [ForeignKey("OrganizationId")]
        public virtual Organization Organization { get; set; } = null!;

        public virtual ICollection<OrganizationalUnit> ChildUnits { get; set; } = new List<OrganizationalUnit>();

        public virtual ICollection<User> Users { get; set; } = new List<User>();

        public virtual ICollection<Initiative> Initiatives { get; set; } = new List<Initiative>();

        // جهات المساندة للمشاريع
    }
}
