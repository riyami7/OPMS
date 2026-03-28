using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// ممثل جهة مساندة — يدعم ممثلين متعددين لكل جهة مساندة في المشروع
    /// </summary>
    [Table("SupportingUnitRepresentatives")]
    public class SupportingUnitRepresentative
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الجهة المساندة في المشروع
        /// </summary>
        [Required]
        public int ProjectSupportingUnitId { get; set; }

        /// <summary>
        /// رقم الموظف (من API)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EmpNumber { get; set; } = string.Empty;

        /// <summary>
        /// اسم الممثل
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// رتبة الممثل
        /// </summary>
        [StringLength(100)]
        public string? Rank { get; set; }

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ========== Navigation ==========

        [ForeignKey("ProjectSupportingUnitId")]
        public virtual ProjectSupportingUnit ProjectSupportingUnit { get; set; } = null!;

        // ========== Helper ==========

        [NotMapped]
        public string FullName => !string.IsNullOrEmpty(Rank)
            ? $"{Rank} {Name}".Trim()
            : Name;
    }
}
