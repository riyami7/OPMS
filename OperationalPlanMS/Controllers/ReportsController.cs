using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Index(Guid? externalUnitId)
        {
            var viewModel = new ReportsDashboardViewModel
            {
                ExternalUnitId = externalUnitId
            };

            if (externalUnitId.HasValue)
            {
                var selectedUnit = await _db.ExternalOrganizationalUnits
                    .FirstOrDefaultAsync(u => u.Id == externalUnitId.Value);
                viewModel.SelectedUnitName = selectedUnit?.ArabicName ?? selectedUnit?.ArabicUnitName;
            }

            // === بناء Query مع فلتر الصلاحيات ===
            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            // صلاحيات: المستخدم يرى فقط ما يخصه
            initiativesQuery = ApplyPermissionFilter(initiativesQuery, userRole, userId);

            // فلتر الوحدة التنظيمية
            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                initiativesQuery = initiativesQuery.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var initiatives = await initiativesQuery.ToListAsync();
            var projects = initiatives.SelectMany(i => i.Projects).ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            // === الإحصائيات الأساسية ===
            viewModel.TotalInitiatives = initiatives.Count;
            viewModel.TotalProjects = projects.Count;
            viewModel.TotalSteps = steps.Count;

            viewModel.OverallProgress = projects.Any()
                ? Math.Round(projects.Average(p => p.ProgressPercentage), 1)
                : 0;

            viewModel.TotalBudget = initiatives.Sum(i => i.Budget ?? 0) + projects.Sum(p => p.Budget ?? 0);
            viewModel.TotalActualCost = initiatives.Sum(i => i.ActualCost ?? 0) + projects.Sum(p => p.ActualCost ?? 0);

            // === حالات المبادرات ===
            viewModel.CompletedInitiatives = initiatives.Count(i =>
                i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100));
            viewModel.InProgressInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100));
            viewModel.NotStartedInitiatives = initiatives.Count(i =>
                !i.Projects.Any() || i.Projects.All(p => p.ProgressPercentage == 0));
            viewModel.DelayedInitiatives = initiatives.Count(i =>
                i.Projects.Any(p => IsProjectDelayed(p)));

            // === حالات المشاريع (منفصلة — بدون عد مزدوج) ===
            viewModel.CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100);
            viewModel.InProgressProjects = projects.Count(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100 && !IsProjectDelayed(p));
            viewModel.DelayedProjects = projects.Count(p => IsProjectDelayed(p));

            // === حالات الخطوات (منفصلة) ===
            viewModel.CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100);
            viewModel.DelayedSteps = steps.Count(s => IsStepDelayed(s));
            viewModel.InProgressSteps = steps.Count(s => s.ProgressPercentage > 0 && s.ProgressPercentage < 100 && !IsStepDelayed(s));
            viewModel.NotStartedSteps = steps.Count(s => s.ProgressPercentage == 0 && !IsStepDelayed(s));

            // === المتأخرات ===
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

            // === مؤشر المخاطر — مشاريع "في خطر" ===
            viewModel.AtRiskProjectsList = BuildAtRiskProjects(projects, initiatives);
            viewModel.AtRiskProjects = viewModel.AtRiskProjectsList.Count;

            // === أفضل وأسوأ المبادرات ===
            viewModel.TopInitiatives = BuildInitiativeRanking(initiatives, top: true);
            viewModel.BottomInitiatives = BuildInitiativeRanking(initiatives, top: false);

            // === أداء المشرفين ===
            viewModel.SupervisorPerformances = BuildSupervisorPerformance(initiatives);

            // === مؤشر الكفاءة الزمنية ===
            viewModel.TimeEfficiencyIndex = CalculateTimeEfficiency(projects);

            // === ملخص الوحدات ===
            viewModel.UnitSummaries = BuildUnitSummaries(initiatives);

            // === منحنى الإنجاز الشهري ===
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

            ViewBag.UserRole = userRole;
            ViewBag.ExternalUnitId = externalUnitId;

            return View(viewModel);
        }

        // GET: /Reports/GetChartData
        [HttpGet]
        public async Task<IActionResult> GetChartData(Guid? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            initiativesQuery = ApplyPermissionFilter(initiativesQuery, userRole, userId);

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
                unitId = (u.ExternalUnitId ?? u.UnitId)?.ToString() ?? ""
            }).ToList();

            // Donut counts — بدون عد مزدوج
            var completed = projects.Count(p => p.ProgressPercentage >= 100);
            var delayed = projects.Count(p => IsProjectDelayed(p));
            var inProgress = projects.Count(p => p.ProgressPercentage > 0 && p.ProgressPercentage < 100 && !IsProjectDelayed(p));
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

        // GET: /Reports/GetSunburstData
        [HttpGet]
        public async Task<IActionResult> GetSunburstData(Guid? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var initiativesQuery = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            initiativesQuery = ApplyPermissionFilter(initiativesQuery, userRole, userId);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                initiativesQuery = initiativesQuery.Where(i =>
                    i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var initiatives = await initiativesQuery.ToListAsync();

            // المبادرات → المشاريع (فقط المبادرات اللي فيها مشاريع)
            var children = initiatives
                .Where(i => i.Projects.Any())
                .Select(i =>
            {
                var avgProgress = Math.Round(i.Projects.Average(p => p.ProgressPercentage), 1);
                var iStatus = i.Projects.All(p => p.ProgressPercentage >= 100) ? "completed"
                    : i.Projects.Any(p => IsProjectDelayed(p)) ? "delayed"
                    : avgProgress > 0 ? "inprogress"
                    : "notstarted";

                return new
                {
                    name = i.NameAr.Length > 20 ? i.NameAr.Substring(0, 20) + "…" : i.NameAr,
                    progress = avgProgress,
                    status = iStatus,
                    children = i.Projects.Select(p => new
                    {
                        name = p.NameAr.Length > 18 ? p.NameAr.Substring(0, 18) + "…" : p.NameAr,
                        value = Math.Max(1, p.Steps.Count),
                        progress = p.ProgressPercentage,
                        status = p.ProgressPercentage >= 100 ? "completed"
                               : IsProjectDelayed(p) ? "delayed"
                               : p.ProgressPercentage > 0 ? "inprogress"
                               : "notstarted",
                        id = p.Id
                    }).ToList()
                };
            }).ToList();

            return Json(children);
        }

        // GET: /Reports/GetGanttData — الجدول الزمني
        [HttpGet]
        public async Task<IActionResult> GetGanttData(Guid? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var query = _db.Projects
                .Where(p => !p.IsDeleted && p.PlannedStartDate.HasValue && p.PlannedEndDate.HasValue)
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            query = ApplyProjectPermissionFilter(query, userRole, userId);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                query = query.Where(p => p.Initiative.ExternalUnitId.HasValue &&
                    unitIds.Contains(p.Initiative.ExternalUnitId.Value));
            }

            var projects = await query.OrderBy(p => p.PlannedStartDate).ToListAsync();

            var data = projects.Select(p => new
            {
                name = p.NameAr.Length > 25 ? p.NameAr.Substring(0, 25) + "…" : p.NameAr,
                plannedStart = p.PlannedStartDate!.Value.ToString("yyyy-MM-dd"),
                plannedEnd = p.PlannedEndDate!.Value.ToString("yyyy-MM-dd"),
                progress = p.ProgressPercentage,
                status = p.ProgressPercentage >= 100 ? "completed"
                       : IsProjectDelayed(p) ? "delayed"
                       : p.ProgressPercentage > 0 ? "inprogress"
                       : "notstarted",
                id = p.Id
            }).ToList();

            return Json(data);
        }

        // GET: /Reports/GetHeatmapData — نشاط شهري
        [HttpGet]
        public async Task<IActionResult> GetHeatmapData(Guid? externalUnitId)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var query = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            query = ApplyPermissionFilter(query, userRole, userId);

            if (externalUnitId.HasValue)
            {
                var unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);
                query = query.Where(i => i.ExternalUnitId.HasValue && unitIds.Contains(i.ExternalUnitId.Value));
            }

            var initiatives = await query.ToListAsync();

            var monthNames = new[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                                     "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

            // لكل مبادرة، نحسب عدد الخطوات المكتملة في كل شهر
            var rows = initiatives
                .Where(i => i.Projects.Any())
                .Select(i =>
                {
                    var steps = i.Projects.SelectMany(p => p.Steps).ToList();
                    var monthly = new int[12];

                    foreach (var s in steps)
                    {
                        // الخطوات المكتملة — نستخدم ActualEndDate كتاريخ الإكمال
                        if (s.ProgressPercentage >= 100 && s.ActualEndDate.HasValue)
                        {
                            var m = s.ActualEndDate.Value.Month - 1;
                            if (m >= 0 && m < 12) monthly[m]++;
                        }
                        // الخطوات الجارية — نحسبها في الشهر الحالي
                        else if (s.ProgressPercentage > 0 && s.ProgressPercentage < 100)
                        {
                            var m = DateTime.Today.Month - 1;
                            if (m >= 0 && m < 12) monthly[m]++;
                        }
                    }

                    return new
                    {
                        name = i.NameAr.Length > 22 ? i.NameAr.Substring(0, 22) + "…" : i.NameAr,
                        totalSteps = steps.Count,
                        data = monthly
                    };
                })
                .Where(r => r.data.Any(v => v > 0))
                .ToList();

            return Json(new { months = monthNames, rows });
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

            if (userRole != UserRole.SuperAdmin && userRole != UserRole.Admin && userRole != UserRole.Executive)
            {
                if (userRole == UserRole.Supervisor && initiative.SupervisorId != userId)
                {
                    var hasAccess = _db.InitiativeAccess.Any(a =>
                        a.InitiativeId == id && a.UserId == userId && a.IsActive);
                    if (!hasAccess) return Forbid();
                }
                else if (userRole != UserRole.Supervisor)
                {
                    // User, StepUser — check project assignment or InitiativeAccess
                    var hasAccess = _db.InitiativeAccess.Any(a =>
                        a.InitiativeId == id && a.UserId == userId && a.IsActive);
                    var isProjectManager = initiative.Projects.Any(p => p.ProjectManagerId == userId);
                    var isStepAssignee = initiative.Projects
                        .SelectMany(p => p.Steps)
                        .Any(s => s.AssignedToId == userId);

                    if (!hasAccess && !isProjectManager && !isStepAssignee) return Forbid();
                }
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

        // GET: /Reports/Export
        [HttpGet]
        public async Task<IActionResult> Export(string type = "initiatives", Guid? externalUnitId = null)
        {
            var csv = new StringBuilder();
            csv.AppendLine("\uFEFF");

            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            List<Guid>? unitIds = null;
            if (externalUnitId.HasValue)
                unitIds = await GetUnitAndChildrenIds(externalUnitId.Value);

            switch (type.ToLower())
            {
                case "initiatives":
                    csv.AppendLine("الكود,الاسم,الوحدة التنظيمية,عدد المشاريع,المكتملة,نسبة الإنجاز,الميزانية,التكلفة الفعلية,الحالة");

                    var initiativesQuery = _db.Initiatives
                        .Where(i => !i.IsDeleted)
                        .Include(i => i.Projects.Where(p => !p.IsDeleted))
                        .AsQueryable();

                    initiativesQuery = ApplyPermissionFilter(initiativesQuery, userRole, userId);

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

                    projectsQuery = ApplyProjectPermissionFilter(projectsQuery, userRole, userId);

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

                    stepsQuery = ApplyStepPermissionFilter(stepsQuery, userRole, userId);

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

                case "overdue":
                    csv.AppendLine("النوع,الكود/الرقم,الاسم,المبادرة/المشروع,المسؤول,نسبة الإنجاز,أيام التأخير");

                    var overdueInitiatives = _db.Initiatives
                        .Where(i => !i.IsDeleted)
                        .Include(i => i.Projects.Where(p => !p.IsDeleted))
                            .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                        .AsQueryable();

                    overdueInitiatives = ApplyPermissionFilter(overdueInitiatives, userRole, userId);

                    var allInitiatives = await overdueInitiatives.ToListAsync();
                    var allProjects = allInitiatives.SelectMany(i => i.Projects).ToList();
                    var allSteps = allProjects.SelectMany(p => p.Steps).ToList();

                    foreach (var p in allProjects.Where(p => IsProjectDelayed(p)))
                    {
                        var days = p.ActualEndDate.HasValue ? Math.Max(0, (DateTime.Today - p.ActualEndDate.Value).Days) : 0;
                        csv.AppendLine($"مشروع,{p.Code},{p.NameAr},{p.Initiative?.NameAr},-,{p.ProgressPercentage}%,{days}");
                    }

                    foreach (var s in allSteps.Where(s => IsStepDelayed(s)))
                    {
                        var days = s.ActualEndDate.HasValue ? Math.Max(0, (DateTime.Today - s.ActualEndDate.Value).Days) : 0;
                        csv.AppendLine($"خطوة,{s.StepNumber},{s.NameAr},{s.Project?.NameAr},{s.AssignedTo?.FullNameAr ?? "-"},{s.ProgressPercentage}%,{days}");
                    }
                    break;

                default:
                    return BadRequest("نوع التقرير غير صالح");
            }

            var fileName = $"Report_{type}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        #region Permission Filters (مركزية)

        private IQueryable<Initiative> ApplyPermissionFilter(IQueryable<Initiative> query, UserRole role, int userId)
        {
            if (role == UserRole.SuperAdmin || role == UserRole.Admin || role == UserRole.Executive)
                return query;

            if (role == UserRole.Supervisor)
            {
                var accessibleIds = _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive)
                    .Select(a => a.InitiativeId);
                return query.Where(i => i.SupervisorId == userId || accessibleIds.Contains(i.Id));
            }

            // User, StepUser — project manager, step assignee, or InitiativeAccess
            var pmInitiativeIds = _db.Projects
                .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                .Select(p => p.InitiativeId)
                .Distinct();

            var stepInitiativeIds = _db.Steps
                .Where(s => s.AssignedToId == userId && !s.IsDeleted)
                .Select(s => s.Project.InitiativeId)
                .Distinct();

            var accessIds = _db.InitiativeAccess
                .Where(a => a.UserId == userId && a.IsActive)
                .Select(a => a.InitiativeId);

            return query.Where(i =>
                pmInitiativeIds.Contains(i.Id) ||
                stepInitiativeIds.Contains(i.Id) ||
                accessIds.Contains(i.Id));
        }

        private IQueryable<Project> ApplyProjectPermissionFilter(IQueryable<Project> query, UserRole role, int userId)
        {
            if (role == UserRole.SuperAdmin || role == UserRole.Admin || role == UserRole.Executive)
                return query;

            if (role == UserRole.Supervisor)
            {
                var supervisedIds = _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id);
                var accessIds = _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive)
                    .Select(a => a.InitiativeId);
                return query.Where(p => supervisedIds.Contains(p.InitiativeId) || accessIds.Contains(p.InitiativeId));
            }

            // User, StepUser
            var pmProjectIds = _db.Projects
                .Where(p2 => p2.ProjectManagerId == userId && !p2.IsDeleted)
                .Select(p2 => p2.Id);
            var stepProjectIds = _db.Steps
                .Where(s => s.AssignedToId == userId && !s.IsDeleted)
                .Select(s => s.ProjectId)
                .Distinct();
            var iaIds = _db.InitiativeAccess
                .Where(a => a.UserId == userId && a.IsActive)
                .Select(a => a.InitiativeId);
            return query.Where(p =>
                pmProjectIds.Contains(p.Id) ||
                stepProjectIds.Contains(p.Id) ||
                iaIds.Contains(p.InitiativeId));
        }

        private IQueryable<Step> ApplyStepPermissionFilter(IQueryable<Step> query, UserRole role, int userId)
        {
            if (role == UserRole.SuperAdmin || role == UserRole.Admin || role == UserRole.Executive)
                return query;

            if (role == UserRole.Supervisor)
            {
                var supervisedIds = _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id);
                var accessIds = _db.InitiativeAccess
                    .Where(a => a.UserId == userId && a.IsActive)
                    .Select(a => a.InitiativeId);
                var projectIds = _db.Projects
                    .Where(p => (supervisedIds.Contains(p.InitiativeId) || accessIds.Contains(p.InitiativeId)) && !p.IsDeleted)
                    .Select(p => p.Id);
                return query.Where(s => projectIds.Contains(s.ProjectId));
            }

            // User, StepUser
            var userProjectIds = _db.Projects
                .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                .Select(p => p.Id);
            var iaIds = _db.InitiativeAccess
                .Where(a => a.UserId == userId && a.IsActive)
                .Select(a => a.InitiativeId);
            var iaProjectIds = _db.Projects
                .Where(p => iaIds.Contains(p.InitiativeId) && !p.IsDeleted)
                .Select(p => p.Id);
            return query.Where(s =>
                s.AssignedToId == userId ||
                userProjectIds.Contains(s.ProjectId) ||
                iaProjectIds.Contains(s.ProjectId));
        }

        #endregion

        #region Builder Methods

        private List<AtRiskProjectItem> BuildAtRiskProjects(List<Project> projects, List<Initiative> initiatives)
        {
            var atRisk = new List<AtRiskProjectItem>();

            foreach (var p in projects)
            {
                // تخطي المكتمل والمتأخر
                if (p.ProgressPercentage >= 100 || IsProjectDelayed(p)) continue;
                if (!p.PlannedStartDate.HasValue || !p.PlannedEndDate.HasValue) continue;

                var totalDays = (p.PlannedEndDate.Value - p.PlannedStartDate.Value).TotalDays;
                if (totalDays <= 0) continue;

                var elapsedDays = (DateTime.Today - p.PlannedStartDate.Value).TotalDays;
                if (elapsedDays <= 0) continue; // لم يبدأ بعد

                var expectedProgress = Math.Min(100, Math.Round((decimal)(elapsedDays / totalDays) * 100, 1));
                var gap = expectedProgress - p.ProgressPercentage;

                // يعتبر "في خطر" إذا الفجوة 10% أو أكثر
                if (gap >= 10)
                {
                    var initiative = initiatives.FirstOrDefault(i => i.Projects.Contains(p));
                    atRisk.Add(new AtRiskProjectItem
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.NameAr,
                        InitiativeName = initiative?.NameAr,
                        SupervisorName = initiative?.SupervisorDisplayName,
                        Progress = p.ProgressPercentage,
                        ExpectedProgress = expectedProgress,
                        DaysRemaining = Math.Max(0, (int)(p.PlannedEndDate.Value - DateTime.Today).TotalDays)
                    });
                }
            }

            return atRisk.OrderByDescending(r => r.Gap).Take(10).ToList();
        }

        private List<SupervisorPerformance> BuildSupervisorPerformance(List<Initiative> initiatives)
        {
            return initiatives
                .Where(i => i.SupervisorId.HasValue && !string.IsNullOrEmpty(i.SupervisorName))
                .GroupBy(i => new { i.SupervisorId, Name = i.SupervisorDisplayName })
                .Select(g => new SupervisorPerformance
                {
                    SupervisorId = g.Key.SupervisorId!.Value,
                    SupervisorName = g.Key.Name,
                    TotalInitiatives = g.Count(),
                    DelayedInitiatives = g.Count(i => i.Projects.Any(p => IsProjectDelayed(p))),
                    CompletedInitiatives = g.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                    AverageProgress = g.SelectMany(i => i.Projects).Any()
                        ? Math.Round(g.SelectMany(i => i.Projects).Average(p => p.ProgressPercentage), 1)
                        : 0
                })
                .OrderBy(s => s.HealthRate)
                .ThenByDescending(s => s.TotalInitiatives)
                .Take(10)
                .ToList();
        }

        private decimal CalculateTimeEfficiency(List<Project> projects)
        {
            var projectsWithDates = projects
                .Where(p => p.PlannedStartDate.HasValue && p.PlannedEndDate.HasValue && p.ProgressPercentage < 100)
                .ToList();

            if (!projectsWithDates.Any()) return 100;

            var efficiencies = new List<decimal>();
            foreach (var p in projectsWithDates)
            {
                var totalDays = (p.PlannedEndDate!.Value - p.PlannedStartDate!.Value).TotalDays;
                if (totalDays <= 0) continue;

                var elapsed = Math.Max(0, (DateTime.Today - p.PlannedStartDate.Value).TotalDays);
                var timePercent = Math.Min(100, (decimal)(elapsed / totalDays) * 100);

                if (timePercent > 0)
                    efficiencies.Add(Math.Min(200, p.ProgressPercentage / timePercent * 100));
            }

            return efficiencies.Any() ? Math.Round(efficiencies.Average(), 0) : 100;
        }

        private List<InitiativeProgressItem> BuildInitiativeRanking(List<Initiative> initiatives, bool top)
        {
            var query = initiatives
                .Where(i => i.Projects.Any());

            if (top)
            {
                query = query.OrderByDescending(i => i.Projects.Average(p => p.ProgressPercentage));
            }
            else
            {
                query = query
                    .Where(i => i.Projects.Any(p => p.ProgressPercentage < 100))
                    .OrderBy(i => i.Projects.Average(p => p.ProgressPercentage));
            }

            return query
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

        private async Task<List<Guid>> GetUnitAndChildrenIds(Guid unitId)
        {
            var allUnits = await _db.ExternalOrganizationalUnits
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.ParentId })
                .ToListAsync();

            var result = new List<Guid> { unitId };
            var children = allUnits.Where(u => u.ParentId == unitId).Select(u => u.Id).ToList();
            result.AddRange(children);
            var grandChildren = allUnits.Where(u => u.ParentId.HasValue && children.Contains(u.ParentId.Value)).Select(u => u.Id).ToList();
            result.AddRange(grandChildren);

            return result;
        }

        #endregion

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
