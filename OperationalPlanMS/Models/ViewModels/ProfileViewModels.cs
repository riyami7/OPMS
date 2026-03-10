using System.ComponentModel.DataAnnotations;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel لعرض الملف الشخصي
    /// </summary>
    public class ProfileViewModel
    {
        public int Id { get; set; }
        public string ADUsername { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FullNameAr { get; set; } = string.Empty;
        public string FullNameEn { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
        public string? RoleName { get; set; }
        // Organization removed - using ExternalUnit directly
        public string? UnitName { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // إحصائيات
        public int SupervisedInitiativesCount { get; set; }
        public int ManagedProjectsCount { get; set; }
        public int AssignedStepsCount { get; set; }
        public int CompletedStepsCount { get; set; }
    }

    /// <summary>
    /// ViewModel لتعديل البيانات الشخصية
    /// </summary>
    public class EditProfileViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم الكامل بالعربية")]
        public string FullNameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم الكامل بالإنجليزية")]
        public string FullNameEn { get; set; } = string.Empty;

        [StringLength(200)]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }
    }

    /// <summary>
    /// ViewModel لتغيير كلمة المرور
    /// </summary>
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الحالية")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الجديدة")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare("NewPassword", ErrorMessage = "كلمة المرور غير متطابقة")]
        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور الجديدة")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
