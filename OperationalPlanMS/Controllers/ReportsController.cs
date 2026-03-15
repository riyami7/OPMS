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
        public async Task<IActionResult> Index(int? fiscalYearId, int? externalUnitId)
        {
            var viewModel = new ReportsDashboardViewModel
            {
                FiscalYearId = fiscalYearId,
                ExternalUnitId = externalUnitId
            };

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

            if (externalUnitId.HasValue)
            {
                var selectedUnit = await _db.ExternalOrganizationalUnits
                    .FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                viewModel.SelectedUnitName = selectedUnit?.ArabicName ?? selectedUnit?.ArabicUnitName;
            }

            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            // StepUser can see reports if they have InitiativeAccess
            if (userRole == UserRole.StepUser)
            {
                var hasAccess = _db.InitiativeAccess.Any(a => a.UserId == userId && a.IsActive);
                if (!hasAccess) return RedirectToAction("Index", "Home");
            }

            if (userRole == UserRole.Supervisor)
            {
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId || accessibleIds.Contains(i.Id));
            }
            else if (userRole == UserRole.User || userRole == UserRole.StepUser)
            {
                var userProjectInitiativeIds = await _db.Projects
                    .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                    .Select(p => p.InitiativeId)
                    .Distinct()
                    .ToListAsync();
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                var allIds = userProjectInitiativeIds.Union(accessibleIds).ToList();
                initiativesQuery = initiativesQuery.Where(i => allIds.Contains(i.Id));
            }

            if (fiscalYearId.HasValue)
                initiativesQuery = initiativesQuery.Where(i => i.FiscalYearId == fiscalYearId.Value);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                initiativesQuery = initiativesQuery.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var initiatives = await initiativesQuery.ToListAsync();
            var projects = initiatives.SelectMany(i => i.Projects).ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            viewModel.TotalInitiatives = initiatives.Count;
            viewModel.TotalProjects = projects.Count;
            viewModel.TotalSteps = steps.Count;

            viewModel.OverallProgress = projects.Any()
                ? Math.Round(projects.Average(p => p.ProgressPercentage), 1)
                : 0;

            viewModel.TotalBudget = initiatives.Sum(i => i.Budget ?? 0) + projects.Sum(p => p.Budget ?? 0);
            viewModel.TotalActualCost = initiatives.Sum(i => i.ActualCost ?? 0) + projects.Sum(p => p.ActualCost ?? 0);

            viewModel.CompletedInitiatives = initiatives.Count(i =>
                i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100));
            viewModel.InProgressInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100));
            viewModel.NotStartedInitiatives = initiatives.Count(i =>
                !i.Projects.Any() || i.Projects.All(p => p.ProgressPercentage == 0));
            viewModel.DelayedInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => IsProjectDelayed(p)));

            viewModel.CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100);
            viewModel.InProgressProjects = projects.Count(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100);
            viewModel.DelayedProjects = projects.Count(p => IsProjectDelayed(p));

            viewModel.CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100);
            viewModel.InProgressSteps = steps.Count(s => s.ProgressPercentage > 0 && s.ProgressPercentage < 100);
            viewModel.NotStartedSteps = steps.Count(s => s.ProgressPercentage == 0);

            viewModel.OverdueInitiatives = initiatives
                .Where(i => i.Projects.Any(p => IsProjectDelayed(p)))
                .OrderByDescending(i => i.Projects.Count(p => IsProjectDelayed(p)))
                .Take(10)
                .ToList();

            viewModel.OverdueProjects = projects
                .Where(p => IsProjectDelayed(p))
                .OrderByDescending(p => p.Steps.Count(s => IsStepDelayed(s)))
                .Take(10)
                .ToList();

            viewModel.OverdueSteps = steps
                .Where(s => IsStepDelayed(s))
                .OrderBy(s => s.ActualEndDate)
                .Take(10)
                .ToList();

            viewModel.TopInitiatives = initiatives
                .Where(i => i.Projects.Any())
                .OrderByDescending(i => i.Projects.Average(p => p.ProgressPercentage))
                .Take(5)
                .Select(i => new InitiativeProgressItem
                {
                    Id = i.Id,
                    Code = i.Code,
                    Name = i.NameAr,
                    UnitName = i.ExternalUnitName ?? "-",
                    Progress = Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1),
                    ProjectsCount = i.Projects.Count,
                    CompletedProjectsCount = i.Projects.Count(p => p.ProgressPercentage >= 100),
                    IsOverdue = i.Projects.Any(p => IsProjectDelayed(p))
                })
                .ToList();

            viewModel.BottomInitiatives = initiatives
                .Where(i => i.Projects.Any() && i.Projects.Any(p => p.ProgressPercentage < 100))
                .OrderBy(i => i.Projects.Average(p => p.ProgressPercentage))
                .Take(5)
                .Select(i => new InitiativeProgressItem
                {
                    Id = i.Id,
                    Code = i.Code,
                    Name = i.NameAr,
                    UnitName = i.ExternalUnitName ?? "-",
                    Progress = Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1),
                    ProjectsCount = i.Projects.Count,
                    CompletedProjectsCount = i.Projects.Count(p => p.ProgressPercentage >= 100),
                    IsOverdue = i.Projects.Any(p => IsProjectDelayed(p))
                })
                .ToList();

            viewModel.UnitSummaries = BuildUnitSummaries(initiatives);

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

            viewModel.FiscalYears = new SelectList(
                await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync(),
                "Id", "NameAr", viewModel.FiscalYearId);

            // Chart data served via /Reports/GetChartData API endpoint

            ViewBag.UserRole = userRole;
            ViewBag.ExternalUnitId = externalUnitId;

            return View(viewModel);
        }

        // GET: /Reports/GetChartData
        [HttpGet]
        public async Task<IActionResult> GetChartData(int? fiscalYearId, int? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.StepUser)
            {
                var hasAccess = _db.InitiativeAccess.Any(a => a.UserId == userId && a.IsActive);
                if (!hasAccess) return Forbid();
            }

            if (!fiscalYearId.HasValue)
            {
                var currentFY = await _db.FiscalYears.FirstOrDefaultAsync(f => f.IsCurrent);
                if (currentFY != null) fiscalYearId = currentFY.Id;
            }

            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            if (userRole == UserRole.Supervisor)
            {
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId || accessibleIds.Contains(i.Id));
            }
            else if (userRole == UserRole.User || userRole == UserRole.StepUser)
            {
                var ids = await _db.Projects
                    .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                    .Select(p => p.InitiativeId).Distinct().ToListAsync();
                var accessibleIds = await _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                var allIds = ids.Union(accessibleIds).ToList();
                initiativesQuery = initiativesQuery.Where(i => allIds.Contains(i.Id));
            }

            if (fiscalYearId.HasValue)
                initiativesQuery = initiativesQuery.Where(i => i.FiscalYearId == fiscalYearId.Value);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                initiativesQuery = initiativesQuery.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var initiatives = await initiativesQuery.ToListAsync();
            var projects = initiatives.SelectMany(i => i.Projects).ToList();

            // Unit performance
            var unitSummaries = BuildUnitSummaries(initiatives).Take(7).ToList();
            var unitData = unitSummaries.Select(u => new {
                label = u.UnitName ?? "",
                value = Math.Round(u.AverageProgress, 1),
                color = u.AverageProgress >= 80 ? "#0e7d5a" :
                         u.AverageProgress >= 50 ? "#1a3a5c" :
                         u.AverageProgress >= 30 ? "#b45309" : "#b91c1c",
                unitId = u.ExternalUnitId ?? u.UnitId ?? 0
            }).ToList();

            // Donut counts
            var completed = projects.Count(p => p.ProgressPercentage >= 100);
            var inProgress = projects.Count(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100);
            var delayed = projects.Count(p => IsProjectDelayed(p));
            var notStarted = projects.Count - completed - inProgress - delayed;
            var overallProg = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0m;

            // Monthly progress
            var monthNames = new[] { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                                      "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
            var monthlyData = Enumerable.Range(1, 12).Select(m => new {
                label = monthNames[m],
                planned = Math.Round((m / 12.0m) * 100, 0),
                actual = m <= DateTime.Today.Month ? overallProg : 0m
            }).ToList();

            return Json(new
            {
                donut = new { completed, inProgress, delayed, notStarted },
                units = unitData,
                monthly = monthlyData
            });
        }

        private async Task<List<int>> GetUnitAndChildrenIds(int unitId)
        {
            // تحميل كل الوحدات النشطة مرة واحدة ثم تصفية في الذاكرة
            var allUnits = await _db.ExternalOrganizationalUnits
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.ParentId })
                .ToListAsync();

            var result = new List<int> { unitId };

            // المستوى الثاني (أبناء مباشرون)
            var children = allUnits.Where(u => u.ParentId == unitId).Select(u => u.Id).ToList();
            result.AddRange(children);

            // المستوى الثالث (أحفاد)
            var grandChildren = allUnits.Where(u => u.ParentId.HasValue && children.Contains(u.ParentId.Value)).Select(u => u.Id).ToList();
            result.AddRange(grandChildren);

            return result;
        }

        private List<UnitSummary> BuildUnitSummaries(List<Initiative> initiatives)
        {
            var summaries = new List<UnitSummary>();

            var groups = initiatives
                .Where(i => i.ExternalUnitId.HasValue)
                .GroupBy(i => new { i.ExternalUnitId, UnitName = i.ExternalUnitName ?? "غير محدد" });

            foreach (var g in groups)
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

            // المبادرات بدون ExternalUnitId
            var noUnitGroup = initiatives.Where(i => !i.ExternalUnitId.HasValue).ToList();
            if (noUnitGroup.Any())
            {
                summaries.Add(new UnitSummary
                {
                    UnitName = "غير محدد",
                    InitiativeCount = noUnitGroup.Count,
                    ProjectCount = noUnitGroup.SelectMany(i => i.Projects).Count(),
                    AverageProgress = noUnitGroup.SelectMany(i => i.Projects).Any()
                        ? Math.Round(noUnitGroup.SelectMany(i => i.Projects).Average(p => p.ProgressPercentage), 1)
                        : 0,
                    CompletedCount = noUnitGroup.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                    DelayedCount = noUnitGroup.Count(i => i.Projects.Any(p => IsProjectDelayed(p))),
                    TotalBudget = noUnitGroup.Sum(i => i.Budget ?? 0) + noUnitGroup.SelectMany(i => i.Projects).Sum(p => p.Budget ?? 0)
                });
            }

            return summaries.OrderByDescending(u => u.InitiativeCount).ToList();
        }

        // GET: /Reports/Export
        [HttpGet]
        public async Task<IActionResult> Export(string type = "initiatives", int? fiscalYearId = null, int? externalUnitId = null)
        {
            var csv = new StringBuilder();
            csv.AppendLine("\uFEFF");

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            List<int>? unitIds = null;
            if (externalUnitId.HasValue)
                unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);

            switch (type.ToLower())
            {
                case "initiatives":
                    csv.AppendLine("الكود,الاسم,الوحدة التنظيمية,عدد المشاريع,المكتملة,نسبة الإنجاز,الميزانية,التكلفة الفعلية,الحالة");

                    var initiativesQuery = _db.Initiatives
                        .Where(i => !i.IsDeleted)
                        .Include(i => i.Projects.Where(p => !p.IsDeleted))
                        .Where(i => !fiscalYearId.HasValue || i.FiscalYearId == fiscalYearId)
                        .AsQueryable();

                    if (userRole == UserRole.Supervisor)
                    {
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        initiativesQuery = initiativesQuery.Where(i => i.SupervisorId == userId || accessIds.Contains(i.Id));
                    }
                    else if (userRole != UserRole.Admin && userRole != UserRole.Executive)
                    {
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        initiativesQuery = initiativesQuery.Where(i => accessIds.Contains(i.Id));
                    }

                    if (unitIds != null)
                        initiativesQuery = initiativesQuery.Where(i =>
                            i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));

                    var exportInitiatives = await initiativesQuery.OrderBy(i => i.Code).ToListAsync();

                    foreach (var i in exportInitiatives)
                    {
                        var projectCount = i.Projects.Count;
                        var completedCount = i.Projects.Count(p => p.ProgressPercentage >= 100);
                        var avgProgress = i.Projects.Any() ? Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1) : 0;
                        var status = GetCalculatedInitiativeStatus(i);
                        var unitName = i.ExternalUnitName ?? "-";
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

                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        var allIds = supervisedIds.Union(accessIds).ToList();
                        projectsQuery = projectsQuery.Where(p => allIds.Contains(p.InitiativeId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == userId || accessIds.Contains(p.InitiativeId));
                    }
                    else if (userRole != UserRole.Admin && userRole != UserRole.Executive)
                    {
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        projectsQuery = projectsQuery.Where(p => accessIds.Contains(p.InitiativeId));
                    }

                    if (unitIds != null)
                        projectsQuery = projectsQuery.Where(p =>
                            p.Initiative.ExternalUnitId.HasValue && unitIds.Contains(p.Initiative.ExternalUnitId.Value));

                    var exportProjects = await projectsQuery.OrderBy(p => p.Code).ToListAsync();

                    foreach (var p in exportProjects)
                    {
                        var stepCount = p.Steps.Count;
                        var completedSteps = p.Steps.Count(s => s.ProgressPercentage >= 100);
                        var status = GetCalculatedProjectStatus(p);
                        var unitName = p.Initiative?.ExternalUnitName ?? "-";
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

                    if (userRole == UserRole.Supervisor)
                    {
                        var supervisedIds = await _db.Initiatives
                            .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                            .Select(i => i.Id).ToListAsync();
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        var allInitIds = supervisedIds.Union(accessIds).ToList();
                        var projectIds = await _db.Projects
                            .Where(p => allInitIds.Contains(p.InitiativeId) && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        stepsQuery = stepsQuery.Where(s => projectIds.Contains(s.ProjectId));
                    }
                    else if (userRole == UserRole.User)
                    {
                        var userProjectIds = await _db.Projects
                            .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        var accessProjectIds = await _db.Projects
                            .Where(p => accessIds.Contains(p.InitiativeId) && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        var allProjIds = userProjectIds.Union(accessProjectIds).ToList();
                        stepsQuery = stepsQuery.Where(s => allProjIds.Contains(s.ProjectId));
                    }
                    else if (userRole != UserRole.Admin && userRole != UserRole.Executive)
                    {
                        var accessIds = await _db.InitiativeAccess
                            .Where(a => a.UserId == userId && a.IsActive).Select(a => a.InitiativeId).ToListAsync();
                        var projectIds = await _db.Projects
                            .Where(p => accessIds.Contains(p.InitiativeId) && !p.IsDeleted)
                            .Select(p => p.Id).ToListAsync();
                        stepsQuery = stepsQuery.Where(s => projectIds.Contains(s.ProjectId));
                    }

                    var exportSteps = await stepsQuery
                        .OrderBy(s => s.Project.InitiativeId)
                        .ThenBy(s => s.ProjectId)
                        .ThenBy(s => s.StepNumber)
                        .ToListAsync();

                    foreach (var s in exportSteps)
                    {
                        var status = GetCalculatedStepStatus(s);
                        var endDate = s.ActualEndDate?.ToString("yyyy-MM-dd") ?? "-";
                        csv.AppendLine($"{s.StepNumber},{s.NameAr},{s.Project?.NameAr},{s.Project?.Initiative?.NameAr},{s.AssignedTo?.FullNameAr ?? "-"},{s.Weight}%,{s.ProgressPercentage}%,{endDate},{status}");
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

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor && initiative.SupervisorId != userId)
            {
                // Check InitiativeAccess as fallback
                var hasAccess = _db.InitiativeAccess.Any(a =>
                    a.InitiativeId == id && a.UserId == userId && a.IsActive);
                if (!hasAccess) return Forbid();
            }
            else if (userRole != UserRole.Admin && userRole != UserRole.Executive && userRole != UserRole.Supervisor)
            {
                // User, StepUser, etc. — check InitiativeAccess
                var hasAccess = _db.InitiativeAccess.Any(a =>
                    a.InitiativeId == id && a.UserId == userId && a.IsActive);
                if (!hasAccess) return Forbid();
            }

            var projects = initiative.Projects.ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            ViewBag.TotalProjects = projects.Count;
            ViewBag.CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100);
            ViewBag.DelayedProjects = projects.Count(p => IsProjectDelayed(p));
            ViewBag.AverageProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            ViewBag.TotalSteps = steps.Count;
            ViewBag.CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100);
            ViewBag.DelayedSteps = steps.Count(s => IsStepDelayed(s));

            ViewBag.Projects = projects;
            ViewBag.Steps = steps;
            ViewBag.UnitName = initiative.ExternalUnitName ?? "-";

            return View(initiative);
        }

        #region Helper Methods

        private bool IsStepDelayed(Step step)
        {
            if (step.ProgressPercentage >= 100) return false;
            if (step.Status == StepStatus.Cancelled) return false;
            if (step.ActualEndDate.HasValue && step.ActualEndDate.Value < DateTime.Today) return true;
            return step.Status == StepStatus.Delayed;
        }

        private bool IsProjectDelayed(Project project)
        {
            if (project.ProgressPercentage >= 100) return false;
            if (project.Steps != null && project.Steps.Any(s => !s.IsDeleted && IsStepDelayed(s))) return true;
            if (project.ActualEndDate.HasValue && project.ActualEndDate.Value < DateTime.Today) return true;
            return false;
        }

        private string GetCalculatedStepStatus(Step step)
        {
            if (step.ProgressPercentage >= 100) return "مكتمل";
            if (step.Status == StepStatus.Cancelled) return "ملغي";
            if (step.Status == StepStatus.OnHold) return "متوقف";
            if (IsStepDelayed(step)) return "متأخر";
            if (step.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        private string GetCalculatedProjectStatus(Project project)
        {
            if (project.ProgressPercentage >= 100) return "مكتمل";
            if (IsProjectDelayed(project)) return "متأخر";
            if (project.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

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