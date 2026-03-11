using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace OperationalPlanMS.Models.ViewModels
{
    #region User ViewModels

    public class UserListViewModel
    {
        public List<User> Users { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? RoleId { get; set; }
        public bool? IsActive { get; set; }
        public SelectList? Roles { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class UserFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الموظف مطلوب")]
        [StringLength(100)]
        [Display(Name = "رقم الموظف")]
        [RegularExpression(@"^[A-Za-z]\d+-\d+$", ErrorMessage = "صيغة رقم الموظف غير صحيحة (مثال: C1-1234)")]
        public string ADUsername { get; set; } = string.Empty;

        [StringLength(200)]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "الاسم الكامل بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم الكامل بالعربية")]
        public string FullNameAr { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "الاسم الكامل بالإنجليزية")]
        public string? FullNameEn { get; set; }

        [Required(ErrorMessage = "الدور مطلوب")]
        [Display(Name = "الدور")]
        public int RoleId { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "مؤكد الخطوات")]
        public bool IsStepApprover { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "الرتبة")]
        public string? EmployeeRank { get; set; }

        [StringLength(200)]
        [Display(Name = "المنصب")]
        public string? EmployeePosition { get; set; }

        [StringLength(200)]
        [Display(Name = "الفرع")]
        public string? BranchName { get; set; }

        [Display(Name = "الوحدة التنظيمية (API)")]
        public int? ExternalUnitId { get; set; }

        [StringLength(300)]
        [Display(Name = "اسم الوحدة (API)")]
        public string? ExternalUnitName { get; set; }

        public SelectList? Roles { get; set; }

        public static UserFormViewModel FromEntity(User entity) => new()
        {
            Id = entity.Id,
            ADUsername = entity.ADUsername,
            Email = entity.Email,
            FullNameAr = entity.FullNameAr,
            FullNameEn = entity.FullNameEn,
            RoleId = entity.RoleId,
            IsActive = entity.IsActive,
            IsStepApprover = entity.IsStepApprover,
            EmployeeRank = entity.EmployeeRank,
            EmployeePosition = entity.EmployeePosition,
            BranchName = entity.BranchName,
            ExternalUnitId = entity.ExternalUnitId,
            ExternalUnitName = entity.ExternalUnitName
        };

        public void UpdateEntity(User entity)
        {
            entity.ADUsername = ADUsername; entity.Email = Email;
            entity.FullNameAr = FullNameAr; entity.FullNameEn = FullNameEn ?? FullNameAr;
            entity.RoleId = RoleId; entity.IsActive = IsActive; entity.IsStepApprover = IsStepApprover;
            entity.EmployeeRank = EmployeeRank; entity.EmployeePosition = EmployeePosition;
            entity.BranchName = BranchName; entity.ExternalUnitId = ExternalUnitId; entity.ExternalUnitName = ExternalUnitName;
        }
    }

    #endregion

    #region FiscalYear ViewModels

    public class FiscalYearListViewModel
    {
        public List<FiscalYear> FiscalYears { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class FiscalYearFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "السنة مطلوبة")]
        [Display(Name = "السنة")]
        public int Year { get; set; } = DateTime.Now.Year;

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(100)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(100)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ البداية")]
        public DateTime StartDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);

        [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ النهاية")]
        public DateTime EndDate { get; set; } = new DateTime(DateTime.Now.Year, 12, 31);

        [Display(Name = "السنة الحالية")]
        public bool IsCurrent { get; set; } = false;

        public static FiscalYearFormViewModel FromEntity(FiscalYear entity) => new()
        {
            Id = entity.Id,
            Year = entity.Year,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent
        };

        public void UpdateEntity(FiscalYear entity)
        {
            entity.Year = Year; entity.NameAr = NameAr; entity.NameEn = NameEn;
            entity.StartDate = StartDate; entity.EndDate = EndDate; entity.IsCurrent = IsCurrent;
        }
    }

    #endregion

    #region Role ViewModels

    public class RoleListViewModel
    {
        public List<Role> Roles { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class RoleFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الكود مطلوب")]
        [StringLength(50)]
        [Display(Name = "الكود")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(100)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(100)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "الصلاحيات")]
        public string? Permissions { get; set; }

        public static RoleFormViewModel FromEntity(Role entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            Permissions = entity.Permissions
        };

        public void UpdateEntity(Role entity)
        {
            entity.Code = Code; entity.NameAr = NameAr;
            entity.NameEn = NameEn; entity.Permissions = Permissions;
        }
    }

    #endregion
}