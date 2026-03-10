using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    /// <summary>
    /// التكلفة المالية
    /// </summary>
    [Table("FinancialCosts")]
    public class FinancialCost
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// اسم التكلفة بالعربية
        /// </summary>
        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        /// <summary>
        /// اسم التكلفة بالإنجليزية
        /// </summary>
        
        [StringLength(200)]
        public string? NameEn { get; set; } = string.Empty;

        /// <summary>
        /// وصف التكلفة بالعربية
        /// </summary>
        public string? DescriptionAr { get; set; }

        /// <summary>
        /// وصف التكلفة بالإنجليزية
        /// </summary>
        public string? DescriptionEn { get; set; }

        /// <summary>
        /// ترتيب العرض
        /// </summary>
        public int OrderIndex { get; set; } = 0;

        /// <summary>
        /// نشط
        /// </summary>
        public bool IsActive { get; set; } = true;

        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? LastModifiedById { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        // Navigation
        [ForeignKey("CreatedById")]
        public virtual User CreatedBy { get; set; } = null!;

        [ForeignKey("LastModifiedById")]
        public virtual User? LastModifiedBy { get; set; }
    }
}
