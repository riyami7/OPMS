using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OperationalPlanMS.Models.Entities
{
    [Table("FiscalYears")]
    public class FiscalYear
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        [StringLength(100)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(100)]
        public string? NameEn { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsCurrent { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedBy { get; set; }

        public virtual ICollection<Initiative> Initiatives { get; set; } = new List<Initiative>();
    }
}
