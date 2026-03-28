using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Project List
    /// </summary>
    public class ProjectListViewModel
    {
        public List<Project> Projects { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int? InitiativeId { get; set; }
        
        public SelectList? Initiatives { get; set; }
        
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// ViewModel for Create/Edit Project
    /// </summary>
    public class ProjectFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الكود مطلوب")]
        [StringLength(50)]
        [Display(Name = "الكود")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم المشروع مطلوب")]
        [StringLength(50)]
        [Display(Name = "رقم المشروع")]
        public string ProjectNumber { get; set; } = string.Empty;

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

        [Display(Name = "الهدف التشغيلي")]
        public string? OperationalGoal { get; set; }

        // ======= التواريخ =======
        [Required(ErrorMessage = "تاريخ البداية المتوقع مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ البداية المتوقع")]
        public DateTime? PlannedStartDate { get; set; }

        [Required(ErrorMessage = "تاريخ النهاية المتوقع مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ النهاية المتوقع")]
        public DateTime? PlannedEndDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاريخ البداية الفعلي")]
        public DateTime? ActualStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاريخ النهاية الفعلي")]
        public DateTime? ActualEndDate { get; set; }

        // ======= الميزانية =======
        [Range(0, double.MaxValue, ErrorMessage = "الميزانية يجب أن تكون قيمة موجبة")]
        [Display(Name = "الميزانية")]
        public decimal? Budget { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "التكلفة الفعلية يجب أن تكون قيمة موجبة")]
        [Display(Name = "التكلفة الفعلية")]
        public decimal? ActualCost { get; set; }

        // ======= معلومات إضافية =======
        [Display(Name = "المخرجات المتوقعة")]
        public string? ExpectedOutcomes { get; set; }

        [Display(Name = "ملاحظات المخاطر")]
        public string? RiskNotes { get; set; }

        // ======= العلاقات الأساسية =======
        [Required(ErrorMessage = "المبادرة مطلوبة")]
        [Display(Name = "المبادرة")]
        public int InitiativeId { get; set; }

        /// <summary>
        /// الوحدة التنظيمية المحلية (اختياري - للتوافق مع البيانات القديمة)
        /// </summary>
        //[Display(Name = "الوحدة التنظيمية المحلية")]

        [Display(Name = "مدير المشروع")]
        public int? ProjectManagerId { get; set; }

        // ======= للعرض فقط =======

        // ======= الهيكل التنظيمي من API =======

        /// <summary>
        /// معرف الوحدة التنظيمية من API (آخر مستوى مختار)
        /// </summary>
        [Display(Name = "الوحدة التنظيمية")]
        public Guid? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة المختارة (للعرض)
        /// </summary>
        public string? ExternalUnitName { get; set; }

        // ======= مدير المشروع من API (جديد) =======

        /// <summary>
        /// رقم مدير المشروع (من API)
        /// </summary>
        [Display(Name = "رقم مدير المشروع")]
        public string? ProjectManagerEmpNumber { get; set; }

        /// <summary>
        /// اسم مدير المشروع (للعرض)
        /// </summary>
        [Display(Name = "مدير المشروع")]
        public string? ProjectManagerName { get; set; }

        /// <summary>
        /// رتبة مدير المشروع
        /// </summary>
        public string? ProjectManagerRank { get; set; }

        // ======= مساعد مدير المشروع (جديد - اختياري) =======

        /// <summary>
        /// رقم مساعد مدير المشروع (من API)
        /// </summary>
        [Display(Name = "رقم مساعد مدير المشروع")]
        public string? DeputyManagerEmpNumber { get; set; }

        /// <summary>
        /// اسم مساعد مدير المشروع (للعرض)
        /// </summary>
        [Display(Name = "مساعد مدير المشروع")]
        public string? DeputyManagerName { get; set; }

        /// <summary>
        /// رتبة مساعد مدير المشروع
        /// </summary>
        public string? DeputyManagerRank { get; set; }

        // ======= الأهداف الفرعية (multi-select) =======

        /// <summary>
        /// الأهداف الفرعية المرتبطة بالمشروع (اختياري، أكثر من هدف)
        /// </summary>
        [Display(Name = "الأهداف الفرعية")]
        public List<int> SubObjectiveIds { get; set; } = new();

        public SelectList? SubObjectives { get; set; }

        // ======= التكلفة المالية (جديد) =======

        /// <summary>
        /// نوع التكلفة المالية
        /// </summary>
        [Display(Name = "التكلفة المالية")]
        public int? FinancialCostId { get; set; }

        public SelectList? FinancialCosts { get; set; }

        // ======= القوائم الديناميكية =======
        
        /// <summary>
        /// متطلبات التنفيذ (قائمة نصوص)
        /// </summary>
        [Display(Name = "متطلبات التنفيذ")]
        public List<string> Requirements { get; set; } = new();

        /// <summary>
        /// مؤشرات الأداء (قائمة)
        /// </summary>
        [Display(Name = "مؤشرات الأداء")]
        public List<KPIItemViewModel> KPIItems { get; set; } = new();

        /// <summary>
        /// جهات المساندة (قائمة معرفات) - النظام القديم
        /// </summary>
        [Display(Name = "الجهات المساندة")]
        public List<int> SupportingEntityIds { get; set; } = new();

        /// <summary>
        /// الجهات المساندة مع الممثلين (متعددين) - من API
        /// </summary>
        [Display(Name = "الجهات المساندة")]
        public List<SupportingEntityWithRepViewModel> SupportingEntitiesWithReps { get; set; } = new();

        /// <summary>
        /// نسب السنوات (للمشاريع متعددة السنوات)
        /// </summary>
        [Display(Name = "نسب السنوات")]
        public List<YearTargetItemViewModel> YearTargets { get; set; } = new();

        // ======= Dropdown lists =======
        public SelectList? Initiatives { get; set; }
        public SelectList? ProjectManagers { get; set; }

        // ======= Computed =======
        
        public bool IsMultiYear
        {
            get
            {
                if (!PlannedStartDate.HasValue || !PlannedEndDate.HasValue) return false;
                return PlannedEndDate.Value.Year > PlannedStartDate.Value.Year;
            }
        }

        public List<int> ProjectYears
        {
            get
            {
                if (!PlannedStartDate.HasValue || !PlannedEndDate.HasValue)
                    return new List<int>();

                var years = new List<int>();
                for (int y = PlannedStartDate.Value.Year; y <= PlannedEndDate.Value.Year; y++)
                {
                    years.Add(y);
                }
                return years;
            }
        }

        /// <summary>
        /// إنشاء ViewModel من Entity
        /// </summary>
        public static ProjectFormViewModel FromEntity(Project entity)
        {
            var vm = new ProjectFormViewModel
            {
                Id = entity.Id,
                Code = entity.Code,
                ProjectNumber = entity.ProjectNumber,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                DescriptionAr = entity.DescriptionAr,
                DescriptionEn = entity.DescriptionEn,
                OperationalGoal = entity.OperationalGoal,
                PlannedStartDate = entity.PlannedStartDate,
                PlannedEndDate = entity.PlannedEndDate,
                ActualStartDate = entity.ActualStartDate,
                ActualEndDate = entity.ActualEndDate,
                Budget = entity.Budget,
                ActualCost = entity.ActualCost,
                ExpectedOutcomes = entity.ExpectedOutcomes,
                RiskNotes = entity.RiskNotes,
                InitiativeId = entity.InitiativeId,
                ProjectManagerId = entity.ProjectManagerId,

                // الحقول الجديدة
                ExternalUnitId = entity.ExternalUnitId,
                ExternalUnitName = entity.ExternalUnitName,
                ProjectManagerEmpNumber = entity.ProjectManagerEmpNumber,
                ProjectManagerName = entity.ProjectManagerName,
                ProjectManagerRank = entity.ProjectManagerRank,
                DeputyManagerEmpNumber = entity.DeputyManagerEmpNumber,
                DeputyManagerName = entity.DeputyManagerName,
                DeputyManagerRank = entity.DeputyManagerRank,
                SubObjectiveIds = entity.ProjectSubObjectives?.Select(ps => ps.SubObjectiveId).ToList() ?? new List<int>(),
                FinancialCostId = entity.FinancialCostId
            };

            // تحميل متطلبات التنفيذ
            if (entity.Requirements != null && entity.Requirements.Any())
            {
                vm.Requirements = entity.Requirements
                    .OrderBy(r => r.OrderIndex)
                    .Select(r => r.RequirementText)
                    .ToList();
            }

            // تحميل مؤشرات الأداء
            if (entity.ProjectKPIs != null && entity.ProjectKPIs.Any())
            {
                vm.KPIItems = entity.ProjectKPIs
                    .OrderBy(k => k.OrderIndex)
                    .Select(k => new KPIItemViewModel
                    {
                        Id = k.Id,
                        KPIText = k.KPIText,
                        TargetValue = k.TargetValue,
                        ActualValue = k.ActualValue
                    })
                    .ToList();
            }

            // تحميل جهات المساندة مع ممثلين متعددين
            if (entity.SupportingUnits != null && entity.SupportingUnits.Any())
            {
                // النظام القديم - فقط الجهات المحلية (SupportingEntityId > 0)
                vm.SupportingEntityIds = entity.SupportingUnits
                    .Where(s => s.SupportingEntityId.HasValue && s.SupportingEntityId.Value > 0)
                    .Select(s => s.SupportingEntityId!.Value)
                    .ToList();

                // النظام الجديد مع ممثلين متعددين
                vm.SupportingEntitiesWithReps = entity.SupportingUnits
                    .Select(s => new SupportingEntityWithRepViewModel
                    {
                        SupportingEntityId = s.SupportingEntityId > 0 ? s.SupportingEntityId : null,
                        ExternalUnitId = s.ExternalUnitId,
                        UnitName = s.ExternalUnitName ?? s.SupportingEntity?.NameAr ?? "",
                        Representatives = s.Representatives != null && s.Representatives.Any()
                            ? s.Representatives.OrderBy(r => r.OrderIndex).Select(r => new RepresentativeViewModel
                            {
                                EmpNumber = r.EmpNumber,
                                Name = r.Name,
                                Rank = r.Rank
                            }).ToList()
                            // Backward compat: إذا ما فيه ممثلين في الجدول الجديد، جرّب القديم
                            : (!string.IsNullOrEmpty(s.RepresentativeEmpNumber)
                                ? new List<RepresentativeViewModel>
                                {
                                    new()
                                    {
                                        EmpNumber = s.RepresentativeEmpNumber!,
                                        Name = s.RepresentativeName ?? "",
                                        Rank = s.RepresentativeRank
                                    }
                                }
                                : new List<RepresentativeViewModel>())
                    })
                    .ToList();
            }

            // تحميل نسب السنوات
            if (entity.YearTargets != null && entity.YearTargets.Any())
            {
                vm.YearTargets = entity.YearTargets
                    .OrderBy(y => y.Year)
                    .Select(y => new YearTargetItemViewModel
                    {
                        Id = y.Id,
                        Year = y.Year,
                        TargetPercentage = y.TargetPercentage,
                        Notes = y.Notes
                    })
                    .ToList();
            }

            return vm;
        }

        /// <summary>
        /// تحديث Entity من ViewModel
        /// </summary>
        public void UpdateEntity(Project entity)
        {
            entity.Code = Code;
            entity.ProjectNumber = ProjectNumber;
            entity.NameAr = NameAr;
            entity.NameEn = NameEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
            entity.OperationalGoal = OperationalGoal;
            entity.PlannedStartDate = PlannedStartDate;
            entity.PlannedEndDate = PlannedEndDate;
            entity.ActualStartDate = ActualStartDate;
            entity.ActualEndDate = ActualEndDate;
            entity.Budget = Budget;
            entity.ActualCost = ActualCost;
            entity.ExpectedOutcomes = ExpectedOutcomes;
            entity.RiskNotes = RiskNotes;
            entity.InitiativeId = InitiativeId;
            entity.ProjectManagerId = ProjectManagerId;

            // الحقول الجديدة
            entity.ExternalUnitId = ExternalUnitId;
            entity.ExternalUnitName = ExternalUnitName;
            entity.ProjectManagerEmpNumber = ProjectManagerEmpNumber;
            entity.ProjectManagerName = ProjectManagerName;
            entity.ProjectManagerRank = ProjectManagerRank;
            entity.DeputyManagerEmpNumber = DeputyManagerEmpNumber;
            entity.DeputyManagerName = DeputyManagerName;
            entity.DeputyManagerRank = DeputyManagerRank;
            // SubObjectives handled separately in Service (many-to-many)
            entity.SubObjectiveId = SubObjectiveIds.FirstOrDefault(); // backward compat
            entity.FinancialCostId = FinancialCostId;

            // قيم افتراضية للحقول القديمة
            entity.Status = Status.InProgress;
            entity.Priority = Priority.Medium;
            entity.Weight = 10;
        }
    }

    /// <summary>
    /// جهة مساندة مع ممثليها (متعددين)
    /// </summary>
    public class SupportingEntityWithRepViewModel
    {
        /// <summary>
        /// معرف الجهة المحلية (إذا من الجدول المحلي)
        /// </summary>
        public int? SupportingEntityId { get; set; }

        /// <summary>
        /// معرف الوحدة من API (إذا من API)
        /// </summary>
        public Guid? ExternalUnitId { get; set; }

        /// <summary>
        /// اسم الوحدة
        /// </summary>
        public string UnitName { get; set; } = string.Empty;

        /// <summary>
        /// ممثلو الجهة (متعددين)
        /// </summary>
        public List<RepresentativeViewModel> Representatives { get; set; } = new();
    }

    /// <summary>
    /// بيانات ممثل واحد
    /// </summary>
    public class RepresentativeViewModel
    {
        /// <summary>
        /// رقم الموظف
        /// </summary>
        public string EmpNumber { get; set; } = string.Empty;

        /// <summary>
        /// اسم الممثل
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// رتبة الممثل
        /// </summary>
        public string? Rank { get; set; }

        /// <summary>
        /// الاسم الكامل
        /// </summary>
        public string FullName => !string.IsNullOrEmpty(Rank)
            ? $"{Rank} {Name}".Trim()
            : Name;
    }

    /// <summary>
    /// عنصر مؤشر أداء واحد
    /// </summary>
    public class KPIItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نص المؤشر مطلوب")]
        [StringLength(500)]
        [Display(Name = "المؤشر")]
        public string KPIText { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "القيمة المستهدفة")]
        public string? TargetValue { get; set; }

        [StringLength(100)]
        [Display(Name = "القيمة الفعلية")]
        public string? ActualValue { get; set; }
    }

    /// <summary>
    /// عنصر نسبة سنة واحدة
    /// </summary>
    public class YearTargetItemViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100")]
        [Display(Name = "النسبة المستهدفة")]
        public decimal TargetPercentage { get; set; }

        [Display(Name = "النسبة الفعلية")]
        public decimal ActualPercentage { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public decimal YearCompletionPercentage
        {
            get
            {
                if (TargetPercentage == 0) return 0;
                return Math.Min((ActualPercentage / TargetPercentage) * 100, 100);
            }
        }

        public bool IsYearCompleted => ActualPercentage >= TargetPercentage;
    }

    /// <summary>
    /// ViewModel for Project Details page
    /// </summary>
    public class ProjectDetailsViewModel
    {
        public Project Project { get; set; } = null!;
        public List<Step> Steps { get; set; } = new();
        public List<ProgressUpdate> Notes { get; set; } = new();
        
        public List<ProjectRequirement> Requirements { get; set; } = new();
        public List<ProjectKPI> KPIs { get; set; } = new();
        public List<SupportingEntityDisplayItem> SupportingEntities { get; set; } = new();
        public List<YearTargetDisplayItem> YearTargets { get; set; } = new();

        // Stats
        public int TotalSteps => Steps.Count;
        public int CompletedSteps => Steps.Count(s => s.ProgressPercentage >= 100);
        public int DelayedSteps => Steps.Count(s => s.IsDelayed);
        public decimal TotalWeight => Steps.Sum(s => s.Weight);

        public decimal CalculatedProgress => Steps
            .Where(s => !s.IsDeleted && s.ProgressPercentage >= 100)
            .Sum(s => s.Weight);

        public bool IsWeightValid => Math.Abs(TotalWeight - 100) < 0.01m;
        public bool IsMultiYear => Project?.IsMultiYear ?? false;
        public decimal TotalYearTargets => YearTargets.Sum(y => y.TargetPercentage);
        public bool IsYearTargetsValid => Math.Abs(TotalYearTargets - 100) < 0.01m;
    }

    /// <summary>
    /// عنصر عرض جهة مساندة مع ممثلين متعددين
    /// </summary>
    public class SupportingEntityDisplayItem
    {
        public string Id { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// ممثلو الجهة (متعددين)
        /// </summary>
        public List<RepresentativeViewModel> Representatives { get; set; } = new();

        // ===== Backward compat (القديم) =====
        public string? RepresentativeEmpNumber { get; set; }
        public string? RepresentativeName { get; set; }
        public string? RepresentativeRank { get; set; }

        /// <summary>
        /// [مهمل] الاسم الكامل للممثل القديم
        /// </summary>
        public string? RepresentativeFullName => 
            string.IsNullOrEmpty(RepresentativeName) ? null : 
            $"{RepresentativeRank} {RepresentativeName}".Trim();

        /// <summary>
        /// كل الممثلين (الجديد + القديم كـ fallback)
        /// </summary>
        public List<RepresentativeViewModel> AllRepresentatives
        {
            get
            {
                if (Representatives.Any()) return Representatives;
                if (!string.IsNullOrEmpty(RepresentativeEmpNumber))
                    return new List<RepresentativeViewModel>
                    {
                        new()
                        {
                            EmpNumber = RepresentativeEmpNumber!,
                            Name = RepresentativeName ?? "",
                            Rank = RepresentativeRank
                        }
                    };
                return new();
            }
        }
    }

    /// <summary>
    /// عنصر عرض نسبة سنة مع الإنجاز
    /// </summary>
    public class YearTargetDisplayItem
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public decimal TargetPercentage { get; set; }
        public decimal ActualPercentage { get; set; }
        public string? Notes { get; set; }

        public decimal YearCompletionPercentage
        {
            get
            {
                if (TargetPercentage == 0) return 0;
                return Math.Min(Math.Round((ActualPercentage / TargetPercentage) * 100, 1), 100);
            }
        }

        public bool IsYearCompleted => ActualPercentage >= TargetPercentage;

        public string ProgressBarClass
        {
            get
            {
                if (IsYearCompleted) return "bg-success";
                if (YearCompletionPercentage >= 70) return "bg-info";
                if (YearCompletionPercentage >= 40) return "bg-warning";
                return "bg-danger";
            }
        }
    }

    /// <summary>
    /// ViewModel for adding a note to project
    /// </summary>
    public class ProjectNoteViewModel
    {
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "الملاحظة مطلوبة")]
        [Display(Name = "الملاحظة")]
        public string Note { get; set; } = string.Empty;
    }
}
