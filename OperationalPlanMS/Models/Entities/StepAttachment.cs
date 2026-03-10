using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// مرفقات الخطوة (لتوثيق الإتمام)
    /// </summary>
    [Table("StepAttachments")]
    public class StepAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StepId { get; set; }

        /// <summary>
        /// اسم الملف المخزن
        /// </summary>
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// اسم الملف الأصلي
        /// </summary>
        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// نوع الملف (pdf, jpg, png, etc.)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// حجم الملف بالبايت
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// مسار الملف
        /// </summary>
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// وصف المرفق
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        public int UploadedById { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("StepId")]
        public virtual Step Step { get; set; } = null!;

        [ForeignKey("UploadedById")]
        public virtual User UploadedBy { get; set; } = null!;
    }
}