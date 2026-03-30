using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    public class DashboardViewModel
    {
        // السنة المالية الحالية
        public FiscalYear? CurrentFiscalYear { get; set; }

        // إحصائيات عامة
        public int TotalInitiatives { get; set; }
        public int TotalProjects { get; set; }
        public int TotalSteps { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrganizations { get; set; }

        // إحصائيات المبادرات
        public int DraftInitiatives { get; set; }
        public int CompletedInitiatives { get; set; }
        public int InProgressInitiatives { get; set; }
        public int DelayedInitiatives { get; set; }
        public int OnHoldInitiatives { get; set; }
        public decimal AverageInitiativeProgress { get; set; }

        // إحصائيات المشاريع
        public int CompletedProjects { get; set; }
        public int InProgressProjects { get; set; }
        public int DelayedProjects { get; set; }
        public decimal AverageProjectProgress { get; set; }

        // إحصائيات الخطوات
        public int CompletedSteps { get; set; }
        public int InProgressSteps { get; set; }
        public int DelayedSteps { get; set; }

        // الميزانية
        public decimal TotalBudget { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal BudgetUtilization => TotalBudget > 0
            ? Math.Round(TotalActualCost / TotalBudget * 100, 1) : 0;

        // قوائم البيانات
        public List<Initiative> RecentInitiatives { get; set; } = new();
        public List<Project> RecentProjects { get; set; } = new();
        public List<Step> MySteps { get; set; } = new();
        public List<Initiative> OverdueInitiatives { get; set; } = new();
        public List<Project> OverdueProjects { get; set; } = new();
        public List<Step> OverdueSteps { get; set; } = new();
        public List<Step> MyUpcomingDeadlines { get; set; } = new();
        public List<Notification> RecentNotifications { get; set; } = new();
        public List<Initiative> UpcomingDeadlines { get; set; } = new();

        // توزيع الحالات (للرسم البياني)
        public Dictionary<string, int> StatusDistribution { get; set; } = new();

        // حسابات مساعدة
        public int PendingInitiatives => TotalInitiatives - CompletedInitiatives - InProgressInitiatives;
        public int PendingProjects => TotalProjects - CompletedProjects - InProgressProjects;

        public decimal InitiativeCompletionRate => TotalInitiatives > 0
            ? Math.Round((decimal)CompletedInitiatives / TotalInitiatives * 100, 1) : 0;

        public decimal ProjectCompletionRate => TotalProjects > 0
            ? Math.Round((decimal)CompletedProjects / TotalProjects * 100, 1) : 0;
    }
}