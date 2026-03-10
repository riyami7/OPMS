using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Roles")]
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string NameEn { get; set; } = string.Empty;

        public string? Permissions { get; set; }

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();

        // Constants
        public const string Admin = "admin";
        public const string Supervisor = "supervisor";
        public const string UserRole = "user";
    }
}