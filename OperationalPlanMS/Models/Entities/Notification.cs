using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// المستخدم المستلم
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// عنوان الإشعار
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// تفاصيل الإشعار
        /// </summary>
        [StringLength(500)]
        public string? Message { get; set; }

        /// <summary>
        /// نوع الإشعار
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = "Info";

        /// <summary>
        /// رابط الإشعار (الصفحة المرتبطة)
        /// </summary>
        [StringLength(300)]
        public string? Url { get; set; }

        /// <summary>
        /// أيقونة الإشعار
        /// </summary>
        [StringLength(50)]
        public string Icon { get; set; } = "bi-bell";

        /// <summary>
        /// هل تم قراءته
        /// </summary>
        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReadAt { get; set; }

        // Navigation
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // Computed
        [NotMapped]
        public string TypeBadgeClass => Type switch
        {
            "StepAssigned" => "text-primary",
            "StepApproval" => "text-warning",
            "StepApproved" => "text-success",
            "StepRejected" => "text-danger",
            "StatusChange" => "text-warning",
            "Note" => "text-info",
            _ => "text-secondary"
        };

        [NotMapped]
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - CreatedAt;
                if (diff.TotalMinutes < 1) return "الآن";
                if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
                if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
                if (diff.TotalDays < 7) return $"منذ {(int)diff.TotalDays} يوم";
                return CreatedAt.ToString("yyyy-MM-dd");
            }
        }
    }
}
