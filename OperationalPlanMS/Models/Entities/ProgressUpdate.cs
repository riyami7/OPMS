using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("ProgressUpdates")]
    public class ProgressUpdate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public UpdateType UpdateType { get; set; } = UpdateType.Regular;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ProgressPercentage { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal PreviousPercentage { get; set; }

        public string? NotesAr { get; set; }

        public string? NotesEn { get; set; }

        public string? Challenges { get; set; }

        public string? NextSteps { get; set; }

        public int? Quarter { get; set; }

        [Column(TypeName = "date")]
        public DateTime? PeriodStart { get; set; }

        [Column(TypeName = "date")]
        public DateTime? PeriodEnd { get; set; }

        public bool IsQuarterlyReport { get; set; } = false;

        public int? InitiativeId { get; set; }

        public int? ProjectId { get; set; }

        public int? StepId { get; set; }

        [Required]
        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("InitiativeId")]
        public virtual Initiative? Initiative { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("StepId")]
        public virtual Step? Step { get; set; }

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;
    }
}