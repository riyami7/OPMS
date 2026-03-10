using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();

            // Get current fiscal year
            viewModel.CurrentFiscalYear = await _db.FiscalYears
                .FirstOrDefaultAsync(f => f.IsCurrent);

            // Initiative Statistics
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .ToListAsync();

            viewModel.TotalInitiatives = initiatives.Count;
            viewModel.DraftInitiatives = initiatives.Count(i => i.Status == Status.Draft);
            viewModel.InProgressInitiatives = initiatives.Count(i => i.Status == Status.InProgress);
            viewModel.CompletedInitiatives = initiatives.Count(i => i.Status == Status.Completed);
            viewModel.DelayedInitiatives = initiatives.Count(i => i.Status == Status.Delayed);
            viewModel.OnHoldInitiatives = initiatives.Count(i => i.Status == Status.OnHold);

            // Average Progress
            if (initiatives.Any())
            {
                viewModel.AverageInitiativeProgress = Math.Round(initiatives.Average(i => i.ProgressPercentage), 1);
            }

            // Budget
            viewModel.TotalBudget = initiatives.Sum(i => i.Budget ?? 0);
            viewModel.TotalActualCost = initiatives.Sum(i => i.ActualCost ?? 0);

            // Project Statistics
            var projects = await _db.Projects
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            viewModel.TotalProjects = projects.Count;
            viewModel.CompletedProjects = projects.Count(p => p.Status == Status.Completed);
            viewModel.InProgressProjects = projects.Count(p => p.Status == Status.InProgress);

            // Step Statistics
            var steps = await _db.Steps
                .Where(s => !s.IsDeleted)
                .ToListAsync();

            viewModel.TotalSteps = steps.Count;
            viewModel.CompletedSteps = steps.Count(s => s.Status == StepStatus.Completed);

            // Recent Initiatives (last 5)
            viewModel.RecentInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.OrganizationalUnit)
                .Include(i => i.Supervisor)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Upcoming Deadlines (next 30 days)
            var today = DateTime.Today;
            var thirtyDaysLater = today.AddDays(30);
            viewModel.UpcomingDeadlines = await _db.Initiatives
                .Where(i => !i.IsDeleted
                    && i.Status != Status.Completed
                    && i.Status != Status.Cancelled
                    && i.PlannedEndDate >= today
                    && i.PlannedEndDate <= thirtyDaysLater)
                .Include(i => i.OrganizationalUnit)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToListAsync();

            // Overdue Initiatives
            viewModel.OverdueInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted
                    && i.Status != Status.Completed
                    && i.Status != Status.Cancelled
                    && i.PlannedEndDate < today)
                .Include(i => i.OrganizationalUnit)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToListAsync();

            // Status Distribution for Chart
            viewModel.StatusDistribution = new Dictionary<string, int>
            {
                { "مسودة", viewModel.DraftInitiatives },
                { "قيد التنفيذ", viewModel.InProgressInitiatives },
                { "مكتمل", viewModel.CompletedInitiatives },
                { "متأخر", viewModel.DelayedInitiatives },
                { "متوقف", viewModel.OnHoldInitiatives }
            };

            return View(viewModel);
        }
    }
}