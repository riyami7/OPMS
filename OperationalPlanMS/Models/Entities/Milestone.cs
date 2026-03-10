using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Milestones")]
    public class Milestone
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameEn { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "date")]
        public DateTime DueDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? CompletedDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        public bool IsDeleted { get; set; } = false;

        [Required]
        public MilestoneType MilestoneType { get; set; } = MilestoneType.Checkpoint;

        [StringLength(500)]
        public string? Deliverable { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;
    }
}