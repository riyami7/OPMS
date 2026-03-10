using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Initiative List
    /// </summary>
    public class InitiativeListViewModel
    {
        public List<Initiative> Initiatives { get; set; } = new();
        public string? SearchTerm { get; set; }
        
        
        public int? FiscalYearId { get; set; }

        
        
        public SelectList? FiscalYears { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// ViewModel for Create/Edit Initiative
    /// يدعم الوحدات التنظيمية والمشرف من API الخارجي
    /// </summary>
    public class InitiativeFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الكود مطلوب")]
        [StringLength(50)]
        [Display(Name = "الكود")]
        public string Code { get; set; } = string.Empty;

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

        // ======= الميزانية =======
        [Range(0, double.MaxValue, ErrorMessage = "الميزانية يجب أن تكون قيمة موجبة")]
        [Display(Name = "الميزانية المعتمدة")]
        public decimal? Budget { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "التكلفة الفعلية يجب أن تكون قيمة موجبة")]
        [Display(Name = "التكلفة الفعلية")]
        public decimal? ActualCost { get; set; }

        [StringLength(500)]
        [Display(Name = "الهدف الاستراتيجي")]
        public string? StrategicObjective { get; set; }

        // ======= العلاقات القديمة (للتوافق) =======
        [Display(Name = "المنظمة")]
        

        [Display(Name = "الوحدة التنظيمية")]
        

        [Display(Name = "المشرف")]
        public int? SupervisorId { get; set; }

        // ======= الهيكل التنظيمي من API (جديد) =======

        /// <summary>
        /// معرف الوحدة من API
        /// </summary>
        [Display(Name = "الوحدة التنظيمية")]
        public int? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة من API
        /// </summary>
        [Display(Name = "اسم الوحدة")]
        public string? ExternalUnitName { get; set; }

        // ======= المشرف من API (جديد) =======

        /// <summary>
        /// رقم المشرف من API
        /// </summary>
        [Display(Name = "رقم المشرف")]
        public string? SupervisorEmpNumber { get; set; }

        /// <summary>
        /// اسم المشرف من API
        /// </summary>
        [Display(Name = "اسم المشرف")]
        public string? SupervisorName { get; set; }

        /// <summary>
        /// رتبة المشرف من API
        /// </summary>
        [Display(Name = "رتبة المشرف")]
        public string? SupervisorRank { get; set; }

        // ======= السنة المالية =======
        [Required(ErrorMessage = "السنة المالية مطلوبة")]
        [Display(Name = "السنة المالية")]
        public int FiscalYearId { get; set; }

        // ======= Dropdown lists =======
        
        
        public SelectList? FiscalYears { get; set; }
        public SelectList? Supervisors { get; set; }

        /// <summary>
        /// إنشاء ViewModel من Entity
        /// </summary>
        public static InitiativeFormViewModel FromEntity(Initiative entity)
        {
            return new InitiativeFormViewModel
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                DescriptionAr = entity.DescriptionAr,
                DescriptionEn = entity.DescriptionEn,
                ActualStartDate = entity.ActualStartDate,
                ActualEndDate = entity.ActualEndDate,
                Budget = entity.Budget,
                ActualCost = entity.ActualCost,
                StrategicObjective = entity.StrategicObjective,
                FiscalYearId = entity.FiscalYearId,
                // الحقول القديمة
                SupervisorId = entity.SupervisorId,
                // الحقول الجديدة من API
                ExternalUnitId = entity.ExternalUnitId,
                ExternalUnitName = entity.ExternalUnitName,
                SupervisorEmpNumber = entity.SupervisorEmpNumber,
                SupervisorName = entity.SupervisorName,
                SupervisorRank = entity.SupervisorRank
            };
        }

        /// <summary>
        /// تحديث Entity من ViewModel
        /// </summary>
        public void UpdateEntity(Initiative entity)
        {
            entity.Code = Code;
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.ActualStartDate = ActualStartDate;
            entity.ActualEndDate = ActualEndDate;
            entity.Budget = Budget;
            entity.ActualCost = ActualCost;
            entity.StrategicObjective = StrategicObjective;
            entity.FiscalYearId = FiscalYearId;

            // الحقول الجديدة من API
            entity.ExternalUnitId = ExternalUnitId;
            entity.ExternalUnitName = ExternalUnitName;
            entity.SupervisorEmpNumber = SupervisorEmpNumber;
            entity.SupervisorName = SupervisorName;
            entity.SupervisorRank = SupervisorRank;

            // الحقول القديمة (للتوافق) - تصفّر إذا استخدمنا API
            if (ExternalUnitId.HasValue)
            {
            }
            else
            {
            }

            if (!string.IsNullOrEmpty(SupervisorEmpNumber))
            {
                entity.SupervisorId = null;
            }
            else
            {
                entity.SupervisorId = SupervisorId;
            }

            // قيم افتراضية للحقول القديمة (للتوافق مع DB)
            entity.Status = Status.InProgress;
            entity.Priority = Priority.Medium;
            entity.PlannedStartDate = ActualStartDate ?? DateTime.Today;
            entity.PlannedEndDate = ActualEndDate ?? DateTime.Today.AddMonths(6);
            entity.ProgressPercentage = 0;
            entity.Weight = 10;
        }
    }

    /// <summary>
    /// ViewModel for Initiative Details page
    /// </summary>
    public class InitiativeDetailsViewModel
    {
        public Initiative Initiative { get; set; } = null!;
        public List<Project> Projects { get; set; } = new();
        public List<ProgressUpdate> Notes { get; set; } = new();

        public int TotalProjects => Projects.Count;
        public int CompletedProjects => Projects.Count(p => p.ProgressPercentage >= 100);
        public decimal TotalBudget => Projects.Sum(p => p.Budget ?? 0);
        public decimal TotalActualCost => Projects.Sum(p => p.ActualCost ?? 0);
        public decimal AverageProgress => Projects.Any()
            ? Math.Round(Projects.Average(p => p.ProgressPercentage), 1) : 0;
    }
}