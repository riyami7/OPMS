using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// عضو فريق الخطوة — يدعم أعضاء متعددين لكل خطوة مع تحديد الدور
    /// </summary>
    [Table("StepTeamMembers")]
    public class StepTeamMember
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الخطوة
        /// </summary>
        [Required]
        public int StepId { get; set; }

        /// <summary>
        /// رقم الموظف (من API)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EmpNumber { get; set; } = string.Empty;

        /// <summary>
        /// اسم العضو
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// رتبة العضو
        /// </summary>
        [StringLength(100)]
        public string? Rank { get; set; }

        /// <summary>
        /// دور العضو في الخطوة (حقل نصي حر — مثل: مراجع، منسق، مشرف)
        /// </summary>
        [StringLength(200)]
        public string? Role { get; set; }

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ========== Navigation ==========

        [ForeignKey("StepId")]
        public virtual Step Step { get; set; } = null!;

        // ========== Helper ==========

        [NotMapped]
        public string FullName => !string.IsNullOrEmpty(Rank)
            ? $"{Rank} {Name}".Trim()
            : Name;
    }
}
