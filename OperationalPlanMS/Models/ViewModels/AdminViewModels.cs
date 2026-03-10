using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace OperationalPlanMS.Models.ViewModels
{
    #region Organization ViewModels

    public class OrganizationListViewModel
    {
        public List<Organization> Organizations { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }
    }

    public class OrganizationFormViewModel
    {
        public int Id { get; set; }

        [StringLength(50)]
        [Display(Name = "الكود")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public static OrganizationFormViewModel FromEntity(Organization entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(Organization entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region OrganizationalUnit ViewModels

    public class OrganizationalUnitListViewModel
    {
        public List<OrganizationalUnit> Units { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? OrganizationId { get; set; }
        public SelectList? Organizations { get; set; }
        public int TotalCount { get; set; }
    }

    public class OrganizationalUnitFormViewModel
    {
        public int Id { get; set; }

        [StringLength(50)]
        [Display(Name = "الكود")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "المنظمة مطلوبة")]
        [Display(Name = "المنظمة")]
        public int OrganizationId { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "جهة مساندة")]

        // Dropdowns
        public SelectList? Organizations { get; set; }

        public static OrganizationalUnitFormViewModel FromEntity(OrganizationalUnit entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            OrganizationId = entity.OrganizationId,
            IsActive = entity.IsActive,
        };

        public void UpdateEntity(OrganizationalUnit entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region User ViewModels

    public class UserListViewModel
    {
        public List<User> Users { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? RoleId { get; set; }
        public int? OrganizationalUnitId { get; set; }
        public bool? IsActive { get; set; }
        public SelectList? Roles { get; set; }
        public SelectList? OrganizationalUnits { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class UserFormViewModel
    {
        public int Id { get; set; }

        /// <summary>
        /// رقم الموظف (مثل C1-1234) - يُستخدم كـ AD Username
        /// </summary>
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

        [Required(ErrorMessage = "الاسم الكامل بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم الكامل بالإنجليزية")]
        public string FullNameEn { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        [Display(Name = "كلمة المرور")]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "كلمة المرور غير متطابقة")]
        [Display(Name = "تأكيد كلمة المرور")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "الدور مطلوب")]
        [Display(Name = "الدور")]
        public int RoleId { get; set; }

        [Display(Name = "الوحدة التنظيمية المحلية")]
        public int? OrganizationalUnitId { get; set; }

        [Required(ErrorMessage = "المنظمة مطلوبة")]
        [Display(Name = "المنظمة")]
        public int OrganizationId { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "مؤكد الخطوات")]
        public bool IsStepApprover { get; set; } = false;

        #region Employee API Fields - حقول من نظام الموارد البشرية

        /// <summary>
        /// الرتبة من API
        /// </summary>
        [StringLength(100)]
        [Display(Name = "الرتبة")]
        public string? EmployeeRank { get; set; }

        /// <summary>
        /// المنصب من API
        /// </summary>
        [StringLength(200)]
        [Display(Name = "المنصب")]
        public string? EmployeePosition { get; set; }

        /// <summary>
        /// اسم الفرع من API
        /// </summary>
        [StringLength(200)]
        [Display(Name = "الفرع")]
        public string? BranchName { get; set; }

        /// <summary>
        /// معرف الوحدة من API
        /// </summary>
        [Display(Name = "الوحدة التنظيمية (API)")]
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة من API
        /// </summary>
        [StringLength(300)]
        [Display(Name = "اسم الوحدة (API)")]
        public string? ExternalUnitName { get; set; }

        #endregion

        #region Dropdowns

        public SelectList? Roles { get; set; }
        public SelectList? OrganizationalUnits { get; set; }
        public SelectList? Organizations { get; set; }

        #endregion

        #region Mapping Methods

        public static UserFormViewModel FromEntity(User entity) => new()
        {
            Id = entity.Id,
            ADUsername = entity.ADUsername,
            Email = entity.Email,
            FullNameAr = entity.FullNameAr,
            FullNameEn = entity.FullNameEn,
            RoleId = entity.RoleId,
            OrganizationalUnitId = entity.OrganizationalUnitId,
            OrganizationId = entity.OrganizationId,
            IsActive = entity.IsActive,
            IsStepApprover = entity.IsStepApprover,
            // API fields
            EmployeeRank = entity.EmployeeRank,
            EmployeePosition = entity.EmployeePosition,
            BranchName = entity.BranchName,
            ExternalUnitId = entity.ExternalUnitId,
            ExternalUnitName = entity.ExternalUnitName
        };

        public void UpdateEntity(User entity)
        {
            entity.ADUsername = ADUsername;
            entity.Email = Email;
            entity.FullNameAr = FullNameAr;
            entity.FullNameEn = FullNameEn;
            entity.RoleId = RoleId;
            entity.OrganizationalUnitId = OrganizationalUnitId;
            entity.OrganizationId = OrganizationId;
            entity.IsActive = IsActive;
            entity.IsStepApprover = IsStepApprover;
            // API fields
            entity.EmployeeRank = EmployeeRank;
            entity.EmployeePosition = EmployeePosition;
            entity.BranchName = BranchName;
            entity.ExternalUnitId = ExternalUnitId;
            entity.ExternalUnitName = ExternalUnitName;
        }

        #endregion
    }

    #endregion

    #region FiscalYear ViewModels

    public class FiscalYearListViewModel
    {
        public List<FiscalYear> FiscalYears { get; set; } = new();
        public int? OrganizationId { get; set; }
        public SelectList? Organizations { get; set; }
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

        [Required(ErrorMessage = "المنظمة مطلوبة")]
        [Display(Name = "المنظمة")]
        public int OrganizationId { get; set; }

        // Dropdowns
        public SelectList? Organizations { get; set; }

        public static FiscalYearFormViewModel FromEntity(FiscalYear entity) => new()
        {
            Id = entity.Id,
            Year = entity.Year,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            OrganizationId = entity.OrganizationId
        };

        public void UpdateEntity(FiscalYear entity)
        {
            entity.Year = Year;
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.StartDate = StartDate;
            entity.EndDate = EndDate;
            entity.IsCurrent = IsCurrent;
            entity.OrganizationId = OrganizationId;
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
            entity.Code = Code;
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.Permissions = Permissions;
        }
    }

    #endregion
}
