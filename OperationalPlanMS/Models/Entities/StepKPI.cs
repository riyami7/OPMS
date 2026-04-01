using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// مؤشر أداء للخطوة
    /// </summary>
    [Table("StepKPIs")]
    public class StepKPI
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StepId { get; set; }

        [Required]
        [StringLength(500)]
        public string Indicator { get; set; } = string.Empty;

        public int OrderIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("StepId")]
        public virtual Step Step { get; set; } = null!;
    }
}
