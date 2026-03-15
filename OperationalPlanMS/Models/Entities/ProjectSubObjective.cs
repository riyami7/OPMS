using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// Many-to-many: Project can have multiple SubObjectives
    /// </summary>
    [Table("ProjectSubObjectives")]
    public class ProjectSubObjective
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int SubObjectiveId { get; set; }

        // Navigation
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("SubObjectiveId")]
        public virtual SubObjective SubObjective { get; set; } = null!;
    }
}
