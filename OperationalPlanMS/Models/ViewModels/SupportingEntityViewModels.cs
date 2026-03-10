using System.ComponentModel.DataAnnotations;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    public class SupportingEntityListViewModel
    {
        public List<SupportingEntity> Entities { get; set; } = new();
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public int TotalCount { get; set; }
    }

    public class SupportingEntityFormViewModel
    {
        public int Id { get; set; }

        [StringLength(50)]
        [Display(Name = "الكود")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public static SupportingEntityFormViewModel FromEntity(SupportingEntity entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(SupportingEntity entity)
        {
            entity.NameAr = NameAr; entity.NameEn = NameEn; entity.IsActive = IsActive;
        }
    }
}