using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// سجل التدقيق — يُسجَّل تلقائياً عند كل عملية مهمة
    /// </summary>
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// نوع الكيان (Initiative, Project, Step)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// معرّف الكيان
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// اسم الكيان (للعرض)
        /// </summary>
        [StringLength(300)]
        public string? EntityName { get; set; }

        /// <summary>
        /// نوع العملية
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// تفاصيل التغيير
        /// </summary>
        [StringLength(2000)]
        public string? Details { get; set; }

        /// <summary>
        /// القيمة القديمة (JSON مختصر)
        /// </summary>
        [StringLength(500)]
        public string? OldValue { get; set; }

        /// <summary>
        /// القيمة الجديدة (JSON مختصر)
        /// </summary>
        [StringLength(500)]
        public string? NewValue { get; set; }

        [Required]
        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// عنوان IP (اختياري)
        /// </summary>
        [StringLength(50)]
        public string? IpAddress { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // ========== Computed ==========

        [NotMapped]
        public string ActionDisplayAr => Action switch
        {
            "Create" => "إنشاء",
            "Update" => "تعديل",
            "Delete" => "حذف",
            "StatusChange" => "تغيير حالة",
            "Approve" => "اعتماد",
            "Reject" => "رفض",
            "UpdateProgress" => "تحديث تقدم",
            "ChangeStatus" => "تغيير حالة",
            _ => Action
        };

        [NotMapped]
        public string EntityTypeDisplayAr => EntityType switch
        {
            "Initiative" => "مبادرة",
            "Project" => "مشروع",
            "Step" => "خطوة",
            _ => EntityType
        };

        [NotMapped]
        public string ActionBadgeClass => Action switch
        {
            "Create" => "bg-success",
            "Update" => "bg-primary",
            "Delete" => "bg-danger",
            "StatusChange" or "ChangeStatus" => "bg-warning text-dark",
            "Approve" => "bg-success",
            "Reject" => "bg-danger",
            "UpdateProgress" => "bg-info",
            _ => "bg-secondary"
        };

        [NotMapped]
        public string EntityIcon => EntityType switch
        {
            "Initiative" => "bi-lightning-charge text-warning",
            "Project" => "bi-folder text-success",
            "Step" => "bi-list-check text-info",
            _ => "bi-circle"
        };
    }
}
