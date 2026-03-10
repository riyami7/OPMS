using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel للتخطيط الاستراتيجي (المحاور والأهداف والقيم)
    /// </summary>
    public class StrategicPlanningViewModel
    {
        public List<StrategicAxis> Axes { get; set; } = new();
        public List<StrategicObjective> StrategicObjectives { get; set; } = new();
        public List<MainObjective> MainObjectives { get; set; } = new();
        public List<SubObjective> SubObjectives { get; set; } = new();
        public List<CoreValue> CoreValues { get; set; } = new();

        // للـ Dropdowns
        public SelectList? AxesDropdown { get; set; }
        public SelectList? StrategicObjectivesDropdown { get; set; }
        public SelectList? MainObjectivesDropdown { get; set; }
        public SelectList? OrganizationalUnitsDropdown { get; set; }
    }

    #region Strategic Axis ViewModels

    public class StrategicAxisFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(300)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(300)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public static StrategicAxisFormViewModel FromEntity(StrategicAxis entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(StrategicAxis entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region Strategic Objective ViewModels

    public class StrategicObjectiveFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        [Required(ErrorMessage = "المحور مطلوب")]
        [Display(Name = "المحور الرئيسي")]
        public int StrategicAxisId { get; set; }

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Dropdown
        public SelectList? Axes { get; set; }

        public static StrategicObjectiveFormViewModel FromEntity(StrategicObjective entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            StrategicAxisId = entity.StrategicAxisId,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(StrategicObjective entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.StrategicAxisId = StrategicAxisId;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region Main Objective ViewModels

    public class MainObjectiveFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        [Required(ErrorMessage = "الهدف الاستراتيجي مطلوب")]
        [Display(Name = "الهدف الاستراتيجي")]
        public int StrategicObjectiveId { get; set; }

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Dropdown
        public SelectList? StrategicObjectives { get; set; }

        public static MainObjectiveFormViewModel FromEntity(MainObjective entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            StrategicObjectiveId = entity.StrategicObjectiveId,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(MainObjective entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.StrategicObjectiveId = StrategicObjectiveId;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region Sub Objective ViewModels

    public class SubObjectiveFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالعربية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم بالإنجليزية مطلوب")]
        [StringLength(500)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        [Required(ErrorMessage = "الهدف الرئيسي مطلوب")]
        [Display(Name = "الهدف الرئيسي")]
        public int MainObjectiveId { get; set; }

        // ========== الوحدة التنظيمية المحلية (للتوافق) ==========
        [Display(Name = "الوحدة التنظيمية")]
        public int? OrganizationalUnitId { get; set; }

        // ========== الهيكل التنظيمي من API (جديد) ==========

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

        // ==========================================================

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Dropdowns
        public SelectList? MainObjectives { get; set; }
        public SelectList? OrganizationalUnits { get; set; }

        public static SubObjectiveFormViewModel FromEntity(SubObjective entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            MainObjectiveId = entity.MainObjectiveId,
            OrganizationalUnitId = entity.OrganizationalUnitId,
            ExternalUnitId = entity.ExternalUnitId,
            ExternalUnitName = entity.ExternalUnitName,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(SubObjective entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.MainObjectiveId = MainObjectiveId;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;

            // الحقول الجديدة من API
            entity.ExternalUnitId = ExternalUnitId;
            entity.ExternalUnitName = ExternalUnitName;

            // الحقل القديم (للتوافق) - يصفّر إذا استخدمنا API
            if (ExternalUnitId.HasValue)
            {
                entity.OrganizationalUnitId = null;
            }
            else
            {
                entity.OrganizationalUnitId = OrganizationalUnitId;
            }
        }
    }

    #endregion

    #region Core Value ViewModels

    public class CoreValueFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم القيمة بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم القيمة بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم القيمة بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم القيمة بالإنجليزية")]
        public string? NameEn { get; set; } = string.Empty;

        [Display(Name = "معنى القيمة بالعربية")]
        public string? MeaningAr { get; set; }

        [Display(Name = "معنى القيمة بالإنجليزية")]
        public string? MeaningEn { get; set; }

        [StringLength(100)]
        [Display(Name = "الأيقونة")]
        public string? Icon { get; set; }

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public static CoreValueFormViewModel FromEntity(CoreValue entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            MeaningAr = entity.MeaningAr,
            MeaningEn = entity.MeaningEn,
            Icon = entity.Icon,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(CoreValue entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.MeaningAr = MeaningAr;
            entity.MeaningEn = MeaningEn;
            entity.Icon = Icon;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;
        }
    }

    #endregion

    #region Financial Cost ViewModels

    public class FinancialCostFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم التكلفة بالعربية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم التكلفة بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم التكلفة بالإنجليزية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم التكلفة بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "الوصف بالعربية")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف بالإنجليزية")]
        public string? DescriptionEn { get; set; }

        [Display(Name = "الترتيب")]
        public int OrderIndex { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public static FinancialCostFormViewModel FromEntity(FinancialCost entity) => new()
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            OrderIndex = entity.OrderIndex,
            IsActive = entity.IsActive
        };

        public void UpdateEntity(FinancialCost entity)
        {
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.OrderIndex = OrderIndex;
            entity.IsActive = IsActive;
        }
    }

    #endregion
}