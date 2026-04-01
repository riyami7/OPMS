using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// جهة مساندة للخطوة (من جهات المشروع)
    /// </summary>
    [Table("StepSupportingUnits")]
    public class StepSupportingUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StepId { get; set; }

        [Required]
        public int ProjectSupportingUnitId { get; set; }

        // Navigation
        [ForeignKey("StepId")]
        public virtual Step Step { get; set; } = null!;

        [ForeignKey("ProjectSupportingUnitId")]
        public virtual ProjectSupportingUnit ProjectSupportingUnit { get; set; } = null!;
    }
}
