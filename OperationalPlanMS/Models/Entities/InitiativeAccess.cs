using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// Grants a user access to a specific initiative with a defined access level.
    /// Many-to-many between Users and Initiatives.
    /// </summary>
    [Table("InitiativeAccess")]
    public class InitiativeAccess
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The user being granted access
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// The initiative they can access
        /// </summary>
        [Required]
        public int InitiativeId { get; set; }

        /// <summary>
        /// Level of access: ReadOnly, Contributor, FullAccess
        /// </summary>
        [Required]
        public AccessLevel AccessLevel { get; set; } = AccessLevel.ReadOnly;

        /// <summary>
        /// Who granted this access (Admin)
        /// </summary>
        public int? GrantedById { get; set; }

        /// <summary>
        /// When access was granted
        /// </summary>
        public DateTime GrantedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Optional notes about why access was granted
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Active flag — allows revoking without deleting
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ═══ Navigation Properties ═══

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("InitiativeId")]
        public virtual Initiative Initiative { get; set; } = null!;

        [ForeignKey("GrantedById")]
        public virtual User? GrantedBy { get; set; }
    }
}
