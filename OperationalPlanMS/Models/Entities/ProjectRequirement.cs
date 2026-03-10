using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// متطلبات التنفيذ للمشروع
    /// </summary>
    [Table("ProjectRequirements")]
    public class ProjectRequirement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(500)]
        public string RequirementText { get; set; } = string.Empty;

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;
    }
}
