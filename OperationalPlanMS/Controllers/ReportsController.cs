using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;
using System.Text;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class ReportsController : BaseController
    {
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Reports
        // تم تعديل الفلتر ليدعم ExternalUnitId من API الخارجي
        public async Task<IActionResult> Index(int? fiscalYearId, int? externalUnitId)
        {
            var viewModel = new ReportsDashboardViewModel
            {
                FiscalYearId = fiscalYearId,
                ExternalUnitId = externalUnitId
            };

            // Get current fiscal year if not specified
            if (!fiscalYearId.HasValue)
            {
                var currentFY = await _db.FiscalYears.FirstOrDefaultAsync(f => f.IsCurrent);
                if (currentFY != null)
                {
                    fiscalYearId = currentFY.Id;
                    viewModel.FiscalYearId = currentFY.Id;
                    viewModel.CurrentFiscalYear = currentFY;
                }
            }
            else
            {
                viewModel.CurrentFiscalYear = await _db.FiscalYears.FindAsync(fiscalYearId);
            }

            // الحصول على اسم الوحدة المختارة
            if (externalUnitId.HasValue)
            {
                var selectedUnit = await _db.ExternalOrganizationalUnits
                    .FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                viewModel.SelectedUnitName = selectedUnit?.ArabicName ?? selectedUnit?.ArabicUnitName;
            }

            // Base queries with filters
            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            // Apply role-based filtering
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor)
            {
                initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId);
            }
            else if (userRole == UserRole.User)
            {
                // User sees only their projects' data
                var userProjectInitiativeIds = await _db.Projects
                    .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                    .Select(p => p.InitiativeId)
                    .Distinct()
                    .ToListAsync();
                initiativesQuery = initiativesQuery.Where(i => userProjectInitiativeIds.Contains(i.Id));
            }

            // Apply fiscal year filter
            if (fiscalYearId.HasValue)
            {
                initiativesQuery = initiativesQuery.Where(i => i.FiscalYearId == fiscalYearId.Value);
            }

            // Apply external unit filter - فلتر الوحدة التنظيمية من API
            if (externalUnitId.HasValue)
            {
                // جلب الوحدة المختارة وجميع الوحدات الفرعية
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                initiativesQuery = initiativesQuery.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            // Load data
            var initiatives = await initiativesQuery.ToListAsync();

            // Get all projects and steps from loaded initiatives
            var projects = initiatives.SelectMany(i => i.Projects).ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            // Summary Statistics
            viewModel.TotalInitiatives = initiatives.Count;
            viewModel.TotalProjects = projects.Count;
            viewModel.TotalSteps = steps.Count;

            // حساب نسبة الإنجاز من المشاريع
            viewModel.OverallProgress = projects.Any()
                ? Math.Round(projects.Average(p => p.ProgressPercentage), 1)
                : 0;

            viewModel.TotalBudget = initiatives.Sum(i => i.Budget ?? 0) + projects.Sum(p => p.Budget ?? 0);
            viewModel.TotalActualCost = initiatives.Sum(i => i.ActualCost ?? 0) + projects.Sum(p => p.ActualCost ?? 0);

            // Initiative Status Counts - محسوبة من المشاريع
            viewModel.CompletedInitiatives = initiatives.Count(i =>
                i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100));
            viewModel.InProgressInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100));
            viewModel.NotStartedInitiatives = initiatives.Count(i =>
                !i.Projects.Any() || i.Projects.All(p => p.ProgressPercentage == 0));

            // المبادرات المتأخرة - لديها مشاريع متأخرة
            viewModel.DelayedInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => IsProjectDelayed(p)));

            // Project Status Counts - محسوبة من الخطوات
            viewModel.CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100);
            viewModel.InProgressProjects = projects.Count(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100);
            viewModel.DelayedProjects = projects.Count(p => IsProjectDelayed(p));

            // Step Status Counts
            viewModel.CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100);
            viewModel.InProgressSteps = steps.Count(s => s.ProgressPercentage > 0 && s.ProgressPercentage < 100);
            viewModel.NotStartedSteps = steps.Count(s => s.ProgressPercentage == 0);

            // Overdue Items - المبادرات المتأخرة (لديها مشاريع متأخرة)
            viewModel.OverdueInitiatives = initiatives
                .Where(i => i.Projects.Any(p => IsProjectDelayed(p)))
                .OrderByDescending(i => i.Projects.Count(p => IsProjectDelayed(p)))
                .Take(10)
                .ToList();

            // المشاريع المتأخرة (لديها خطوات متأخرة أو تجاوزت تاريخ النهاية)
            viewModel.OverdueProjects = projects
                .Where(p => IsProjectDelayed(p))
                .OrderByDescending(p => p.Steps.Count(s => IsStepDelayed(s)))
                .Take(10)
                .ToList();

            // الخطوات المتأخرة
            viewModel.OverdueSteps = steps
                .Where(s => IsStepDelayed(s))
                .OrderBy(s => s.ActualEndDate)
                .Take(10)
                .ToList();

            // Top Performing Initiatives - بناءً على متوسط إنجاز المشاريع
            viewModel.TopInitiatives = initiatives
                .Where(i => i.Projects.Any())
                .OrderByDescending(i => i.Projects.Average(p => p.ProgressPercentage))
                .Take(5)
                .Select(i => new InitiativeProgressItem
                {
                    Id = i.Id,
                    Code = i.Code,
                    Name = i.NameAr,
                    // استخدام اسم الوحدة من API أو المحلي
                    UnitName = !string.IsNullOrEmpty(i.ExternalUnitName)
                        ? i.ExternalUnitName
                    Progress = Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1),
                    ProjectsCount = i.Projects.Count,
                    CompletedProjectsCount = i.Projects.Count(p => p.ProgressPercentage >= 100),
                    IsOverdue = i.Projects.Any(p => IsProjectDelayed(p))
                })
                .ToList();

            // Bottom Performing Initiatives (excluding completed)
            viewModel.BottomInitiatives = initiatives
                .Where(i => i.Projects.Any() && i.Projects.Any(p => p.ProgressPercentage < 100))
                .OrderBy(i => i.Projects.Average(p => p.ProgressPercentage))
                .Take(5)
                .Select(i => new InitiativeProgressItem
                {
                    Id = i.Id,
                    Code = i.Code,
                    Name = i.NameAr,
                    UnitName = !string.IsNullOrEmpty(i.ExternalUnitName)
                        ? i.ExternalUnitName
                    Progress = Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1),
                    ProjectsCount = i.Projects.Count,
                    CompletedProjectsCount = i.Projects.Count(p => p.ProgressPercentage >= 100),
                    IsOverdue = i.Projects.Any(p => IsProjectDelayed(p))
                })
                .ToList();

            // Summary by External Organizational Unit - التجميع حسب الوحدات من API
            viewModel.UnitSummaries = await BuildUnitSummaries(initiatives);

            // Monthly Progress Data (for current year)
            var monthNames = new[] { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                                      "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

            for (int month = 1; month <= 12; month++)
            {
                var expectedProgress = (month / 12.0m) * 100;

                viewModel.MonthlyProgressData.Add(new MonthlyProgress
                {
                    Month = month,
                    MonthName = monthNames[month],
                    PlannedProgress = Math.Round(expectedProgress, 0),
                    ActualProgress = month <= DateTime.Today.Month ? viewModel.OverallProgress : 0,
                    CompletedCount = month <= DateTime.Today.Month ? viewModel.CompletedInitiatives : 0
                });
            }

            // Populate dropdowns
            viewModel.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                "Id", "NameAr", viewModel.FiscalYearId);

            // الوحدات المحلية للتوافقية (قد تُحذف لاحقاً)

            ViewBag.UserRole = userRole;
            ViewBag.ExternalUnitId = externalUnitId;

            return View(viewModel);
        }

        /// <summary>
        /// جلب الوحدة وجميع الوحدات الفرعية (للفلترة الهرمية)
        /// </summary>
        private async Task<List<int>> GetUnitAndChildrenIds(int unitId)
        {
            var result = new List<int> { unitId };

            // جلب الأبناء المباشرين
            var children = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == unitId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var childId in children)
            {
                result.Add(childId);

                // جلب أحفاد (المستوى الثالث)
                var grandChildren = await _db.ExternalOrganizationalUnits
                    .Where(u => u.ParentId == childId && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                result.AddRange(grandChildren);
            }

            return result;
        }

        /// <summary>
        /// بناء ملخص الوحدات من API الخارجي
        /// </summary>
        private async Task<List<UnitSummary>> BuildUnitSummaries(List<Initiative> initiatives)
        {
            // تجميع المبادرات حسب ExternalUnitId أو OrganizationalUnitId
            var summaries = new List<UnitSummary>();

            // المبادرات التي لديها ExternalUnitId
            var externalGroups = initiatives
                .Where(i => i.ExternalUnitId.HasValue)
                .GroupBy(i => new { i.ExternalUnitId, UnitName = i.ExternalUnitName ?? "غير محدد" });

            foreach (var g in externalGroups)
            {
                summaries.Add(new UnitSummary
                {
                    ExternalUnitId = g.Key.ExternalUnitId,
                    UnitName = g.Key.UnitName,
                    InitiativeCount = g.Count(),
                    ProjectCount = g.SelectMany(i => i.Projects).Count(),
                    AverageProgress = g.SelectMany(i => i.Projects).Any()
                        ? Math.Round(g.SelectMany(i => i.Projects).Average(p => p.ProgressPercentage), 1)
                        : 0,
                    CompletedCount = g.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                    DelayedCount = g.Count(i => i.Projects.Any(p => IsProjectDelayed(p))),
                    TotalBudget = g.Sum(i => i.Budget ?? 0) + g.SelectMany(i => i.Projects).Sum(p => p.Budget ?? 0)
                });
            }

            // المبادرات التي لديها OrganizationalUnitId المحلي فقط (للتوافقية)
            var localGroups = initiatives
                .Where(i => !i.ExternalUnitId.HasValue && i.OrganizationalUnitId.HasValue)
                .GroupBy(i => new { i.ExternalUnitId, UnitName = i.ExternalUnitName ?? "غير محدد" });

            foreach (var g in localGroups)
            {
                summaries.Add(new UnitSummary
                {
                    UnitId = g.Key.ExternalUnitId,
                    UnitName = g.Key.UnitName,
                    InitiativeCount = g.Count(),
                    ProjectCount = g.SelectMany(i => i.Projects).Count(),
                    AverageProgress = g.SelectMany(i => i.Projects).Any()
                        ? Math.Round(g.SelectMany(i => i.Projects).Average(p => p.ProgressPercentage), 1)
                        : 0,
                    CompletedCount = g.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                    DelayedCount = g.Count(i => i.Projects.Any(p => IsProjectDelayed(p))),
                    TotalBudget = g.Sum(i => i.Budget ?? 0) + g.SelectMany(i => i.Projects).Sum(p => p.Budget ?? 0)
                });
            }

            return summaries.OrderByDescending(u => u.InitiativeCount).ToList();
        }

        // GET: /Reports/Export
        [HttpGet]
        public async Task<IActionResult> Export(string type = "initiatives", int? fiscalYearId = null, int? externalUnitId = null)
        {
            var csv = new StringBuilder();
            csv.AppendLine("\uFEFF"); // BOM for Arabic support in Excel

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            // تحضير فلتر الوحدات
            List<int>? unitIds = null;
            if (externalUnitId.HasValue)
            {
                unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
            }

            switch (type.ToLower())
            {
                case "initiatives":
                    csv.AppendLine("الكود,الاسم,الوحدة التنظيمية,عدد المشاريع,المكتملة,نسبة الإنجاز,الميزانية,التكلفة الفعلية,الحالة");

                    var initiativesQuery = _db.Initiatives
                        .Where(i => !i.IsDeleted)
                        .Include(i => i.Projects.Where(p => !p.IsDeleted))
                        .Where(i => !fiscalYearId.HasValue || i.FiscalYearId == fiscalYearId)
                        .AsQueryable();

                    // Role filter
                    if (userRole == UserRole.Supervisor)
                        initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId);

                    // Unit filter
                    if (unitIds != null)
                        initiativesQuery = initiativesQuery.Where(i =>
                            i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));

                    var initiatives = await initiativesQuery.OrderBy(i => i.Code).ToListAsync();

                    foreach (var i in initiatives)
                    {
                        var projectCount = i.Projects.Count;
                        var completedCount = i.Projects.Count(p => p.ProgressPercentage >= 100);
                        var avgProgress = i.Projects.Any() ? Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1) : 0;
                        var status = GetCalculatedInitiativeStatus(i);
                        var unitName = !string.IsNullOrEmpty(i.ExternalUnitName)
                            ? i.ExternalUnitName

                        csv.AppendLine($"{i.Code},{i.NameAr},{unitName},{projectCount},{completedCount},{avgProgress}%,{i.Budget ?? 0},{i.ActualCost ?? 0},{status}");
                    }
                    break;

                case "projects":
                    csv.AppendLine("الكود,الاسم,المبادرة,الوحدة التنظيمية,عدد الخطوات,المكتملة,نسبة الإنجاز,الميزانية,التكلفة الفعلية,الحالة");

                    var projectsQuery = _db.Projects
                        .Where(p => !p.IsDeleted)
                        .Include(p => p.Initiative)
                        .Include(p => p.Steps.Where(s => !s.IsDeleted))
                        .AsQueryable();

                    // Role filter
                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        projectsQuery = projectsQuery.Where(p => supervisedIds.Contains(p.InitiativeId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == userId);
                    }

                    // Unit filter
                    if (unitIds != null)
                        projectsQuery = projectsQuery.Where(p =>
                            p.Initiative.ExternalUnitId.HasValue && unitIds.Contains(p.Initiative.ExternalUnitId.Value));

                    var projects = await projectsQuery.OrderBy(p => p.Code).ToListAsync();

                    foreach (var p in projects)
                    {
                        var stepCount = p.Steps.Count;
                        var completedSteps = p.Steps.Count(s => s.ProgressPercentage >= 100);
                        var status = GetCalculatedProjectStatus(p);
                        var unitName = !string.IsNullOrEmpty(p.Initiative?.ExternalUnitName)
                            ? p.Initiative.ExternalUnitName
                            : "-";

                        csv.AppendLine($"{p.Code},{p.NameAr},{p.Initiative?.NameAr},{unitName},{stepCount},{completedSteps},{p.ProgressPercentage}%,{p.Budget ?? 0},{p.ActualCost ?? 0},{status}");
                    }
                    break;

                case "steps":
                    csv.AppendLine("رقم الخطوة,الاسم,المشروع,المبادرة,المسؤول,الوزن,نسبة الإنجاز,تاريخ النهاية,الحالة");

                    var stepsQuery = _db.Steps
                        .Where(s => !s.IsDeleted)
                        .Include(s => s.Project)
                            .ThenInclude(p => p.Initiative)
                        .Include(s => s.AssignedTo)
                        .AsQueryable();

                    // Role filter
                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        var projectIds = await _db.Projects
                            .Where(p => supervisedIds.Contains(p.InitiativeId) && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        stepsQuery = stepsQuery.Where(s => projectIds.Contains(s.ProjectId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        var userProjectIds = await _db.Projects
                            .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        stepsQuery = stepsQuery.Where(s => userProjectIds.Contains(s.ProjectId));
                    }

                    var steps = await stepsQuery
                        .OrderBy(s => s.Project.InitiativeId)
                        .ThenBy(s => s.ProjectId)
                        .ThenBy(s => s.StepNumber)
                        .ToListAsync();

                    foreach (var s in steps)
                    {
                        var status = GetCalculatedStepStatus(s);
                        var endDate = s.ActualEndDate?.ToString("yyyy-MM-dd") ?? "-";

                        csv.AppendLine($"{s.StepNumber},{s.NameAr},{s.Project?.NameAr},{s.Project?.Initiative?.NameAr},{s.AssignedTo?.FullNameAr ?? "-"},{s.Weight}%,{s.ProgressPercentage}%,{endDate},{status}");
                    }
                    break;

                case "overdue":
                    csv.AppendLine("النوع,الكود/الرقم,الاسم,الوحدة التنظيمية,تاريخ النهاية,نسبة الإنجاز,الحالة");

                    // المشاريع المتأخرة
                    var overdueProjectsQuery = _db.Projects
                        .Where(p => !p.IsDeleted)
                        .Include(p => p.Initiative)
                        .Include(p => p.Steps.Where(s => !s.IsDeleted))
                        .AsQueryable();

                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        overdueProjectsQuery = overdueProjectsQuery.Where(p => supervisedIds.Contains(p.InitiativeId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        overdueProjectsQuery = overdueProjectsQuery.Where(p => p.ProjectManagerId == userId);
                    }

                    if (unitIds != null)
                        overdueProjectsQuery = overdueProjectsQuery.Where(p =>
                            p.Initiative.ExternalUnitId.HasValue && unitIds.Contains(p.Initiative.ExternalUnitId.Value));

                    var overdueProjects = await overdueProjectsQuery.ToListAsync();
                    overdueProjects = overdueProjects.Where(p => IsProjectDelayed(p)).ToList();

                    foreach (var p in overdueProjects)
                    {
                        var endDate = p.ActualEndDate?.ToString("yyyy-MM-dd") ?? "-";
                        var unitName = !string.IsNullOrEmpty(p.Initiative?.ExternalUnitName)
                            ? p.Initiative.ExternalUnitName
                            : "-";
                        csv.AppendLine($"مشروع,{p.Code},{p.NameAr},{unitName},{endDate},{p.ProgressPercentage}%,متأخر");
                    }

                    // الخطوات المتأخرة
                    var overdueStepsQuery = _db.Steps
                        .Where(s => !s.IsDeleted)
                        .Include(s => s.Project)
                            .ThenInclude(p => p.Initiative)
                        .AsQueryable();

                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        var projectIds = await _db.Projects
                            .Where(p => supervisedIds.Contains(p.InitiativeId) && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        overdueStepsQuery = overdueStepsQuery.Where(s => projectIds.Contains(s.ProjectId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        var userProjectIds = await _db.Projects
                            .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        overdueStepsQuery = overdueStepsQuery.Where(s => userProjectIds.Contains(s.ProjectId));
                    }

                    var overdueSteps = await overdueStepsQuery.ToListAsync();
                    overdueSteps = overdueSteps.Where(s => IsStepDelayed(s)).ToList();

                    foreach (var s in overdueSteps)
                    {
                        var endDate = s.ActualEndDate?.ToString("yyyy-MM-dd") ?? "-";
                        csv.AppendLine($"خطوة,{s.StepNumber},{s.NameAr} ({s.Project?.NameAr}),-,{endDate},{s.ProgressPercentage}%,متأخر");
                    }
                    break;

                default:
                    return BadRequest("نوع التقرير غير صالح");
            }

            var fileName = $"Report_{type}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // GET: /Reports/InitiativeDetails/5
        public async Task<IActionResult> InitiativeDetails(int id)
        {
            var initiative = await _db.Initiatives
                .Include(i => i.FiscalYear)
                .Include(i => i.Supervisor)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (initiative == null)
                return NotFound();

            // Check access
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor && initiative.SupervisorId != userId)
                return Forbid();

            var projects = initiative.Projects.ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            // حساب الإحصائيات
            ViewBag.TotalProjects = projects.Count;
            ViewBag.CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100);
            ViewBag.DelayedProjects = projects.Count(p => IsProjectDelayed(p));
            ViewBag.AverageProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            ViewBag.TotalSteps = steps.Count;
            ViewBag.CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100);
            ViewBag.DelayedSteps = steps.Count(s => IsStepDelayed(s));

            ViewBag.Projects = projects;
            ViewBag.Steps = steps;

            // اسم الوحدة من API أو المحلي
            ViewBag.UnitName = !string.IsNullOrEmpty(initiative.ExternalUnitName)
                ? initiative.ExternalUnitName

            return View(initiative);
        }

        #region Helper Methods

        /// <summary>
        /// تحديد إذا كانت الخطوة متأخرة
        /// </summary>
        private bool IsStepDelayed(Step step)
        {
            if (step.ProgressPercentage >= 100) return false;
            if (step.Status == StepStatus.Cancelled) return false;

            // متأخرة إذا تجاوز تاريخ النهاية الفعلي
            if (step.ActualEndDate.HasValue && step.ActualEndDate.Value < DateTime.Today)
                return true;

            // أو إذا كانت الحالة متأخرة
            return step.Status == StepStatus.Delayed;
        }

        /// <summary>
        /// تحديد إذا كان المشروع متأخر
        /// </summary>
        private bool IsProjectDelayed(Project project)
        {
            if (project.ProgressPercentage >= 100) return false;

            // متأخر إذا لديه خطوات متأخرة
            if (project.Steps != null && project.Steps.Any(s => !s.IsDeleted && IsStepDelayed(s)))
                return true;

            // أو إذا تجاوز تاريخ النهاية الفعلي
            if (project.ActualEndDate.HasValue && project.ActualEndDate.Value < DateTime.Today)
                return true;

            return false;
        }

        /// <summary>
        /// الحصول على حالة الخطوة محسوبة
        /// </summary>
        private string GetCalculatedStepStatus(Step step)
        {
            if (step.ProgressPercentage >= 100) return "مكتمل";
            if (step.Status == StepStatus.Cancelled) return "ملغي";
            if (step.Status == StepStatus.OnHold) return "متوقف";
            if (IsStepDelayed(step)) return "متأخر";
            if (step.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        /// <summary>
        /// الحصول على حالة المشروع محسوبة
        /// </summary>
        private string GetCalculatedProjectStatus(Project project)
        {
            if (project.ProgressPercentage >= 100) return "مكتمل";
            if (IsProjectDelayed(project)) return "متأخر";
            if (project.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        /// <summary>
        /// الحصول على حالة المبادرة محسوبة
        /// </summary>
        private string GetCalculatedInitiativeStatus(Initiative initiative)
        {
            if (!initiative.Projects.Any()) return "لم يبدأ";
            if (initiative.Projects.All(p => p.ProgressPercentage >= 100)) return "مكتمل";
            if (initiative.Projects.Any(p => IsProjectDelayed(p))) return "متأخر";
            if (initiative.Projects.Any(p => p.ProgressPercentage > 0)) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        #endregion
    }
}
