using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Documents")]
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public DocumentCategory Category { get; set; } = DocumentCategory.General;

        [StringLength(500)]
        public string? DescriptionAr { get; set; }

        [StringLength(500)]
        public string? DescriptionEn { get; set; }

        public int Version { get; set; } = 1;

        public int? PreviousVersionId { get; set; }

        public int? InitiativeId { get; set; }

        public int? ProjectId { get; set; }

        [Required]
        public int UploadedById { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey("PreviousVersionId")]
        public virtual Document? PreviousVersion { get; set; }

        [ForeignKey("InitiativeId")]
        public virtual Initiative? Initiative { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("UploadedById")]
        public virtual User UploadedBy { get; set; } = null!;

        public virtual ICollection<Document> NewerVersions { get; set; } = new List<Document>();
    }
}