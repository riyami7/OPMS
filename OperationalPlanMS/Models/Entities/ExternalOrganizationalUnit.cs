using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// الوحدات التنظيمية من API الخارجي (HR System)
    /// الهيكل هرمي: المستوى 1 ← المستوى 2 ← المستوى 3
    /// </summary>
    [Table("ExternalOrganizationalUnits")]
    public class ExternalOrganizationalUnit
    {
        /// <summary>
        /// المعرف - نفس ID من API الخارجي
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // لا يُولّد تلقائياً - يأتي من API
        public int Id { get; set; }

        /// <summary>
        /// معرف الوحدة الأب (للهيكل الهرمي)
        /// null أو 0 = المستوى الأول (الجذر)
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// معرف المستأجر من API
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// كود الوحدة
        /// </summary>
        [StringLength(50)]
        public string? Code { get; set; }

        /// <summary>
        /// الاسم بالعربية
        /// </summary>
        [Required]
        [StringLength(300)]
        public string ArabicName { get; set; } = string.Empty;

        /// <summary>
        /// اسم الوحدة بالعربية (حقل إضافي من API)
        /// </summary>
        [StringLength(300)]
        public string? ArabicUnitName { get; set; }

        /// <summary>
        /// نشط
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// تاريخ آخر مزامنة مع API
        /// </summary>
        public DateTime LastSyncAt { get; set; } = DateTime.Now;

        // ========== Navigation Properties ==========

        /// <summary>
        /// الوحدة الأب
        /// </summary>
        [ForeignKey("ParentId")]
        public virtual ExternalOrganizationalUnit? Parent { get; set; }

        /// <summary>
        /// الوحدات الفرعية
        /// </summary>
        public virtual ICollection<ExternalOrganizationalUnit> Children { get; set; } = new List<ExternalOrganizationalUnit>();

        /// <summary>
        /// المشاريع المرتبطة بهذه الوحدة
        /// </summary>
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

        // ========== Computed Properties ==========

        /// <summary>
        /// هل هذه وحدة جذر (المستوى الأول)
        /// </summary>
        [NotMapped]
        public bool IsRoot => !ParentId.HasValue || ParentId == 0;

        /// <summary>
        /// الاسم للعرض (يفضل ArabicName)
        /// </summary>
        [NotMapped]
        public string DisplayName => !string.IsNullOrEmpty(ArabicName) ? ArabicName : ArabicUnitName ?? "";
    }
}
