using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// إعدادات النظام العامة (الرؤية والمهمة)
    /// </summary>
    [Table("SystemSettings")]
    public class SystemSettings
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// الرؤية بالعربية
        /// </summary>
        [StringLength(1000)]
        public string? VisionAr { get; set; }

        /// <summary>
        /// الرؤية بالإنجليزية
        /// </summary>
        [StringLength(1000)]
        public string? VisionEn { get; set; }

        /// <summary>
        /// المهمة بالعربية
        /// </summary>
        [StringLength(1000)]
        public string? MissionAr { get; set; }

        /// <summary>
        /// المهمة بالإنجليزية
        /// </summary>
        [StringLength(1000)]
        public string? MissionEn { get; set; }

        /// <summary>
        /// وصف إضافي بالعربية
        /// </summary>
        public string? DescriptionAr { get; set; }

        /// <summary>
        /// وصف إضافي بالإنجليزية
        /// </summary>
        public string? DescriptionEn { get; set; }

        public int? LastModifiedById { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        // Navigation
        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }
    }
}