using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Organizations")]
    public class Organization
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

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }

        // Navigation properties
        public virtual ICollection<OrganizationalUnit> OrganizationalUnits { get; set; } = new List<OrganizationalUnit>();
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<FiscalYear> FiscalYears { get; set; } = new List<FiscalYear>();
    }
}