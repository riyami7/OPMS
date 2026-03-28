using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    public class ReportsDashboardViewModel
    {
        // Filters
        public int? FiscalYearId { get; set; }

        // الوحدة التنظيمية المحلية (للتوافقية)

        // الوحدة التنظيمية من API الخارجي - الفلتر الفعلي
        public Guid? ExternalUnitId { get; set; }

        // اسم الوحدة المختارة للعرض
        public string? SelectedUnitName { get; set; }

        public FiscalYear? CurrentFiscalYear { get; set; }

        // Dropdowns
        public SelectList? FiscalYears { get; set; }

        // Summary Statistics
        public int TotalInitiatives { get; set; }
        public int TotalProjects { get; set; }
        public int TotalSteps { get; set; }
        public decimal OverallProgress { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalActualCost { get; set; }

        // نسبة استخدام الميزانية
        public decimal BudgetUtilization => TotalBudget > 0
            ? Math.Round(TotalActualCost / TotalBudget * 100, 1)
            : 0;

        // نسب الإكمال
        public decimal InitiativeCompletionRate => TotalInitiatives > 0
            ? Math.Round((decimal)CompletedInitiatives / TotalInitiatives * 100, 1)
            : 0;

        public decimal ProjectCompletionRate => TotalProjects > 0
            ? Math.Round((decimal)CompletedProjects / TotalProjects * 100, 1)
            : 0;

        public decimal StepCompletionRate => TotalSteps > 0
            ? Math.Round((decimal)CompletedSteps / TotalSteps * 100, 1)
            : 0;

        // Initiative Counts (محسوبة من المشاريع)
        public int CompletedInitiatives { get; set; }
        public int InProgressInitiatives { get; set; }
        public int DelayedInitiatives { get; set; }
        public int NotStartedInitiatives { get; set; }

        // Project Counts (محسوبة من الخطوات)
        public int CompletedProjects { get; set; }
        public int InProgressProjects { get; set; }
        public int DelayedProjects { get; set; }

        // Step Counts
        public int CompletedSteps { get; set; }
        public int InProgressSteps { get; set; }
        public int NotStartedSteps { get; set; }

        // Overdue Items
        public List<Initiative> OverdueInitiatives { get; set; } = new();
        public List<Project> OverdueProjects { get; set; } = new();
        public List<Step> OverdueSteps { get; set; } = new();

        // Performance Rankings
        public List<InitiativeProgressItem> TopInitiatives { get; set; } = new();
        public List<InitiativeProgressItem> BottomInitiatives { get; set; } = new();

        // Unit Summary - يستخدم ExternalUnitId الآن
        public List<UnitSummary> UnitSummaries { get; set; } = new();

        // Monthly Progress
        public List<MonthlyProgress> MonthlyProgressData { get; set; } = new();

        public int NotStartedProjects => TotalProjects - CompletedProjects - InProgressProjects - DelayedProjects;
    }

    public class InitiativeProgressItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? UnitName { get; set; }
        public decimal Progress { get; set; }

        // الحقول الجديدة بدلاً من Status
        public int ProjectsCount { get; set; }
        public int CompletedProjectsCount { get; set; }
        public bool IsOverdue { get; set; }

        // حالة محسوبة
        public string CalculatedStatus =>
            ProjectsCount == 0 ? "لم يبدأ" :
            CompletedProjectsCount == ProjectsCount ? "مكتمل" :
            IsOverdue ? "متأخر" :
            Progress > 0 ? "قيد التنفيذ" : "لم يبدأ";

        public string StatusBadgeClass =>
            CalculatedStatus switch
            {
                "مكتمل" => "badge-completed",
                "متأخر" => "badge-delayed",
                "قيد التنفيذ" => "badge-inprogress",
                _ => "badge-draft"
            };

        // Legacy - للتوافقية مع Views القديمة
        public Status Status { get; set; }
        public int DaysRemaining { get; set; }
    }

    public class UnitSummary
    {
        // الوحدة المحلية (للتوافقية)
        public Guid? UnitId { get; set; }

        // الوحدة من API الخارجي
        public Guid? ExternalUnitId { get; set; }

        public string UnitName { get; set; } = string.Empty;
        public int InitiativeCount { get; set; }
        public int ProjectCount { get; set; }
        public decimal AverageProgress { get; set; }
        public int CompletedCount { get; set; }
        public int DelayedCount { get; set; }
        public decimal TotalBudget { get; set; }

        // نسبة الإنجاز كنص
        public string ProgressText => $"{AverageProgress}%";

        // Badge class
        public string ProgressBadgeClass =>
            AverageProgress >= 100 ? "bg-success" :
            AverageProgress >= 70 ? "bg-info" :
            AverageProgress >= 40 ? "bg-warning" : "bg-danger";
    }

    public class MonthlyProgress
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal PlannedProgress { get; set; }
        public decimal ActualProgress { get; set; }
        public int CompletedCount { get; set; }

        // الفرق بين المخطط والفعلي
        public decimal Variance => ActualProgress - PlannedProgress;
        public bool IsOnTrack => Variance >= 0;
    }

    // ViewModel لتفاصيل المبادرة في التقارير
    public class InitiativeReportDetailsViewModel
    {
        public Initiative Initiative { get; set; } = null!;
        public List<Project> Projects { get; set; } = new();
        public List<Step> Steps { get; set; } = new();

        // إحصائيات
        public int TotalProjects => Projects.Count;
        public int CompletedProjects => Projects.Count(p => p.ProgressPercentage >= 100);
        public int DelayedProjects { get; set; }
        public decimal AverageProgress => Projects.Any()
            ? Math.Round(Projects.Average(p => p.ProgressPercentage), 1)
            : 0;

        public int TotalSteps => Steps.Count;
        public int CompletedSteps => Steps.Count(s => s.ProgressPercentage >= 100);
        public int DelayedSteps { get; set; }

        public decimal TotalBudget => (Initiative.Budget ?? 0) + Projects.Sum(p => p.Budget ?? 0);
        public decimal TotalActualCost => (Initiative.ActualCost ?? 0) + Projects.Sum(p => p.ActualCost ?? 0);
        public decimal BudgetUtilization => TotalBudget > 0
            ? Math.Round(TotalActualCost / TotalBudget * 100, 1)
            : 0;
    }

    // ViewModel للمشاريع المتأخرة
    public class OverdueProjectViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? InitiativeName { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Progress { get; set; }
        public int DelayedStepsCount { get; set; }
        public int TotalStepsCount { get; set; }

        public int DaysOverdue => EndDate.HasValue
            ? Math.Max(0, (DateTime.Today - EndDate.Value).Days)
            : 0;
    }

    // ViewModel للخطوات المتأخرة
    public class OverdueStepViewModel
    {
        public int Id { get; set; }
        public int StepNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProjectName { get; set; }
        public string? InitiativeName { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Progress { get; set; }
        public decimal Weight { get; set; }

        public int DaysOverdue => EndDate.HasValue
            ? Math.Max(0, (DateTime.Today - EndDate.Value).Days)
            : 0;
    }
}