using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Step List
    /// </summary>
    public class StepListViewModel
    {
        public List<Step> Steps { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? ProjectId { get; set; }
        public StepStatus? StatusFilter { get; set; }

        public SelectList? Projects { get; set; }
        public SelectList? Statuses { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// ViewModel for Create/Edit Step
    /// التعديل: إزالة PlannedDates، إبقاء Weight و ProgressPercentage
    /// </summary>
    public class StepFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الخطوة مطلوب")]
        [Display(Name = "رقم الخطوة")]
        public int StepNumber { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        // ======= التواريخ الفعلية فقط =======
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ البداية الفعلي")]
        public DateTime? ActualStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاريخ النهاية الفعلي")]
        public DateTime? ActualEndDate { get; set; }

        // ======= الوزن ونسبة الإنجاز =======
        [Required(ErrorMessage = "الوزن مطلوب")]
        [Range(0, 100, ErrorMessage = "الوزن يجب أن يكون بين 0 و 100")]
        [Display(Name = "الوزن (من 100)")]
        public decimal Weight { get; set; } = 10;

        [Range(0, 100, ErrorMessage = "نسبة الإنجاز يجب أن تكون بين 0 و 100")]
        [Display(Name = "نسبة الإنجاز")]
        public decimal ProgressPercentage { get; set; } = 0;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // ======= العلاقات =======
        [Required(ErrorMessage = "المشروع مطلوب")]
        [Display(Name = "المشروع")]
        public int ProjectId { get; set; }

        [Display(Name = "المسؤول")]
        public int? AssignedToId { get; set; }

        [Display(Name = "تعتمد على خطوة")]
        public int? DependsOnStepId { get; set; }

        // ======= Dropdown lists =======
        public SelectList? Projects { get; set; }
        public SelectList? Users { get; set; }
        public SelectList? DependsOnSteps { get; set; }

        // ======= للعرض فقط =======
        public string? ProjectName { get; set; }

        // ======= مسؤول الخطوة من API (جديد) =======

        /// <summary>
        /// رقم الموظف المسؤول (من API)
        /// </summary>
        [Display(Name = "رقم الموظف")]
        public string? AssignedToEmpNumber { get; set; }

        /// <summary>
        /// اسم الموظف المسؤول (من API)
        /// </summary>
        [Display(Name = "اسم المسؤول")]
        public string? AssignedToName { get; set; }

        /// <summary>
        /// رتبة الموظف المسؤول (من API)
        /// </summary>
        [Display(Name = "الرتبة")]
        public string? AssignedToRank { get; set; }

        /// <summary>
        /// إنشاء ViewModel من Entity
        /// </summary>
        public static StepFormViewModel FromEntity(Step entity)
        {
            return new StepFormViewModel
            {
                Id = entity.Id,
                StepNumber = entity.StepNumber,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                DescriptionAr = entity.DescriptionAr,
                DescriptionEn = entity.DescriptionEn,
                ActualStartDate = entity.ActualStartDate,
                ActualEndDate = entity.ActualEndDate,
                Weight = entity.Weight,
                ProgressPercentage = entity.ProgressPercentage,
                Notes = entity.Notes,
                ProjectId = entity.ProjectId,
                AssignedToId = entity.AssignedToId,
                DependsOnStepId = entity.DependsOnStepId,
                ProjectName = entity.Project?.NameAr,
                // ========== الحقول الجديدة ==========
                AssignedToEmpNumber = entity.AssignedToEmpNumber,
                AssignedToName = entity.AssignedToName,
                AssignedToRank = entity.AssignedToRank
            };
        }

        /// <summary>
        /// تحديث Entity من ViewModel
        /// </summary>
        public void UpdateEntity(Step entity)
        {
            entity.StepNumber = StepNumber;
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.ActualStartDate = ActualStartDate;
            entity.ActualEndDate = ActualEndDate;
            entity.Weight = Weight;
            entity.ProgressPercentage = ProgressPercentage;
            entity.Notes = Notes;
            entity.ProjectId = ProjectId;
            entity.AssignedToId = AssignedToId;
            entity.DependsOnStepId = DependsOnStepId;

            // ========== الحقول الجديدة - مسؤول الخطوة من API ==========
            entity.AssignedToEmpNumber = AssignedToEmpNumber;
            entity.AssignedToName = AssignedToName;
            entity.AssignedToRank = AssignedToRank;

            // قيم افتراضية للحقول القديمة (للتوافق مع DB)
            entity.PlannedStartDate = ActualStartDate ?? DateTime.Today;
            entity.PlannedEndDate = ActualEndDate ?? DateTime.Today.AddDays(7);

            // تحديث الحالة تلقائياً
            entity.Status = entity.CalculatedStatus;
        }
    }

    /// <summary>
    /// ViewModel for Step Details page
    /// </summary>
    public class StepDetailsViewModel
    {
        public Step Step { get; set; } = null!;
        public List<ProgressUpdate> Notes { get; set; } = new();

        public bool IsDelayed => Step.IsDelayed;
        public bool IsCompleted => Step.IsCompleted;

        public string StatusText => Step.CalculatedStatus switch
        {
            StepStatus.NotStarted => "لم تبدأ",
            StepStatus.InProgress => "جارية",
            StepStatus.Completed => "مكتملة",
            StepStatus.OnHold => "متوقفة",
            StepStatus.Cancelled => "ملغاة",
            StepStatus.Delayed => "متأخرة",
            _ => "غير محدد"
        };

        public string StatusBadgeClass => Step.CalculatedStatus switch
        {
            StepStatus.NotStarted => "bg-secondary",
            StepStatus.InProgress => "bg-primary",
            StepStatus.Completed => "bg-success",
            StepStatus.OnHold => "bg-warning",
            StepStatus.Cancelled => "bg-dark",
            StepStatus.Delayed => "bg-danger",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// ViewModel for updating step progress
    /// </summary>
    public class StepProgressUpdateViewModel
    {
        public int StepId { get; set; }

        [Required]
        [Range(0, 100)]
        [Display(Name = "نسبة الإنجاز")]
        public decimal ProgressPercentage { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// ViewModel for adding a note to step
    /// </summary>
    public class StepNoteViewModel
    {
        public int StepId { get; set; }

        [Required(ErrorMessage = "الملاحظة مطلوبة")]
        [Display(Name = "الملاحظة")]
        public string Note { get; set; } = string.Empty;
    }
}