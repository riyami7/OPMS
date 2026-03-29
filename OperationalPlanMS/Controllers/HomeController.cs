using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;
using System.Security.Claims;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext db, ILogger<HomeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Get current user info from claims
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;
            var roleNameAr = User.FindFirst("RoleNameAr")?.Value;
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var employeeRank = User.FindFirst("EmployeeRank")?.Value;
            int.TryParse(userIdStr, out int userId);
            Enum.TryParse<UserRole>(userRoleStr, out UserRole userRole);

            ViewBag.UserName = userName;
            ViewBag.UserRole = userRole;
            ViewBag.RoleNameAr = roleNameAr;
            ViewBag.EmployeeRank = employeeRank;

            try
            {
                var viewModel = new DashboardViewModel();

                switch (userRole)
                {
                    case UserRole.Admin:
                        await LoadAdminDashboard(viewModel);
                        break;
                    case UserRole.Executive:
                        await LoadExecutiveDashboard(viewModel);
                        break;
                    case UserRole.Supervisor:
                        await LoadSupervisorDashboard(viewModel, userId);
                        break;
                    case UserRole.User:
                        await LoadUserDashboard(viewModel, userId);
                        break;
                    case UserRole.StepUser:
                        // StepUser يُوجَّه مباشرة لأول مشروع معيّن له
                        var stepUserProject = await _db.Steps
                            .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                            .Select(s => s.ProjectId)
                            .FirstOrDefaultAsync();
                        if (stepUserProject != 0)
                            return RedirectToAction("Details", "Projects", new { id = stepUserProject });
                        await LoadStepUserDashboard(viewModel, userId);
                        break;
                    default:
                        await LoadBasicDashboard(viewModel);
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تحميل لوحة التحكم");
                ViewBag.DatabaseError = "حدث خطأ في الاتصال بقاعدة البيانات. يرجى المحاولة لاحقاً.";
                return View(new DashboardViewModel());
            }
        }

        #region Dashboard Loaders

        private async Task LoadBasicDashboard(DashboardViewModel model)
        {
            model.TotalInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted);
            model.TotalProjects = await _db.Projects.CountAsync(p => !p.IsDeleted);
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);
        }

        private async Task LoadAdminDashboard(DashboardViewModel model)
        {
            // تحميل البيانات في أقل عدد من الاستعلامات (5 بدل 14)
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Supervisor)
                .ToListAsync();

            var projects = await _db.Projects
                .Where(p => !p.IsDeleted)
                .Include(p => p.Initiative)
                .ToListAsync();

            var totalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);
            var totalUsers = await _db.Users.CountAsync(u => u.IsActive);

            // الإحصائيات — محسوبة في الذاكرة
            model.TotalInitiatives = initiatives.Count;
            model.TotalProjects = projects.Count;
            model.TotalSteps = totalSteps;
            model.TotalUsers = totalUsers;

            model.CompletedInitiatives = initiatives.Count(i => i.Status == Status.Completed);
            model.InProgressInitiatives = initiatives.Count(i => i.Status == Status.InProgress);
            model.DelayedInitiatives = initiatives.Count(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = projects.Count(i => i.Status == Status.Completed);
            model.InProgressProjects = projects.Count(i => i.Status == Status.InProgress);

            model.AverageInitiativeProgress = initiatives.Any()
                ? Math.Round(initiatives.Average(i => i.ProgressPercentage), 1) : 0;
            model.AverageProjectProgress = projects.Any()
                ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            // القوائم — مرتبة من البيانات المحمّلة
            model.RecentInitiatives = initiatives
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList();

            model.RecentProjects = projects
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToList();

            model.OverdueInitiatives = initiatives
                .Where(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToList();
        }

        private async Task LoadExecutiveDashboard(DashboardViewModel model)
        {
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Supervisor)
                .ToListAsync();

            var projects = await _db.Projects
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            model.TotalInitiatives = initiatives.Count;
            model.TotalProjects = projects.Count;
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);

            model.CompletedInitiatives = initiatives.Count(i => i.Status == Status.Completed);
            model.InProgressInitiatives = initiatives.Count(i => i.Status == Status.InProgress);
            model.DelayedInitiatives = initiatives.Count(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = projects.Count(p => p.Status == Status.Completed);
            model.InProgressProjects = projects.Count(p => p.Status == Status.InProgress);

            model.AverageInitiativeProgress = initiatives.Any()
                ? Math.Round(initiatives.Average(i => i.ProgressPercentage), 1) : 0;
            model.AverageProjectProgress = projects.Any()
                ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            model.RecentInitiatives = initiatives
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList();

            model.OverdueInitiatives = initiatives
                .Where(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToList();
        }

        private async Task LoadSupervisorDashboard(DashboardViewModel model, int userId)
        {
            // تحميل مبادراتي مع المشرف
            var myInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.SupervisorId == userId)
                .ToListAsync();

            var myInitiativeIds = myInitiatives.Select(i => i.Id).ToList();

            // تحميل مشاريع مبادراتي
            var myProjects = await _db.Projects
                .Where(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId))
                .Include(p => p.Initiative)
                .Include(p => p.ProjectManager)
                .ToListAsync();

            var myProjectIds = myProjects.Select(p => p.Id).ToList();

            model.TotalInitiatives = myInitiatives.Count;
            model.TotalProjects = myProjects.Count;
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted && myProjectIds.Contains(s.ProjectId));

            model.CompletedInitiatives = myInitiatives.Count(i => i.Status == Status.Completed);
            model.InProgressInitiatives = myInitiatives.Count(i => i.Status == Status.InProgress);
            model.DelayedInitiatives = myInitiatives.Count(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = myProjects.Count(p => p.Status == Status.Completed);
            model.InProgressProjects = myProjects.Count(p => p.Status == Status.InProgress);

            model.AverageInitiativeProgress = myInitiatives.Any()
                ? Math.Round(myInitiatives.Average(i => i.ProgressPercentage), 1) : 0;

            model.RecentInitiatives = myInitiatives
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList();

            model.RecentProjects = myProjects
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToList();

            model.OverdueInitiatives = myInitiatives
                .Where(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToList();
        }

        private async Task LoadUserDashboard(DashboardViewModel model, int userId)
        {
            // تحميل مشاريعي
            var myProjects = await _db.Projects
                .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                .Include(p => p.Initiative)
                .ToListAsync();

            var myProjectIds = myProjects.Select(p => p.Id).ToList();

            // تحميل خطواتي
            var mySteps = await _db.Steps
                .Where(s => !s.IsDeleted && (s.AssignedToId == userId || myProjectIds.Contains(s.ProjectId)))
                .Include(s => s.Project)
                .ToListAsync();

            model.TotalProjects = myProjects.Count;
            model.TotalSteps = mySteps.Count(s => myProjectIds.Contains(s.ProjectId));

            model.CompletedProjects = myProjects.Count(p => p.Status == Status.Completed);
            model.InProgressProjects = myProjects.Count(p => p.Status == Status.InProgress);
            model.DelayedProjects = myProjects.Count(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed);

            model.CompletedSteps = mySteps.Count(s => myProjectIds.Contains(s.ProjectId) && s.Status == StepStatus.Completed);
            model.InProgressSteps = mySteps.Count(s => myProjectIds.Contains(s.ProjectId) && s.Status == StepStatus.InProgress);

            model.AverageProjectProgress = myProjects.Any()
                ? Math.Round(myProjects.Average(p => p.ProgressPercentage), 1) : 0;

            model.RecentProjects = myProjects
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToList();

            model.MySteps = mySteps
                .OrderBy(s => s.PlannedEndDate)
                .Take(10)
                .ToList();

            model.OverdueProjects = myProjects
                .Where(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed)
                .OrderBy(p => p.PlannedEndDate)
                .Take(5)
                .ToList();
        }

        private async Task LoadStepUserDashboard(DashboardViewModel model, int userId)
        {
            var myStepIds = await _db.Steps
                .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                .Select(s => s.Id)
                .ToListAsync();

            model.TotalSteps = myStepIds.Count;
            model.CompletedSteps = await _db.Steps.CountAsync(s => myStepIds.Contains(s.Id) && s.Status == StepStatus.Completed);
            model.InProgressSteps = await _db.Steps.CountAsync(s => myStepIds.Contains(s.Id) && s.Status == StepStatus.InProgress);

            model.MySteps = await _db.Steps
                .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                .Include(s => s.Project)
                .OrderBy(s => s.PlannedEndDate)
                .Take(10)
                .ToListAsync();
        }

        #endregion



        /// <summary>
        /// الصفحة الرئيسية للخطة الاستراتيجية
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> StrategicOverview()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                SystemSettings = await _db.SystemSettings.FirstOrDefaultAsync(),

                // تحميل إعدادات الوحدات مع دعم الـ API الخارجي
                UnitSettings = await _db.OrganizationalUnitSettings
                    .ToListAsync(),

                Axes = await _db.StrategicAxes
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.OrderIndex)
                    .ToListAsync(),

                StrategicObjectives = await _db.StrategicObjectives
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync(),

                MainObjectives = await _db.MainObjectives
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.OrderIndex)
                    .ToListAsync(),

                // تحميل الأهداف الفرعية مع دعم الـ API الخارجي
                SubObjectives = await _db.SubObjectives
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync(),

                CoreValues = await _db.CoreValues
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.OrderIndex)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// صفحة الرؤية والمهمة
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> VisionMission()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                SystemSettings = await _db.SystemSettings.FirstOrDefaultAsync(),

                // تحميل إعدادات الوحدات مع دعم الـ API الخارجي
                UnitSettings = await _db.OrganizationalUnitSettings
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// صفحة المحاور والأهداف
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> StrategicObjectives()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                Axes = await _db.StrategicAxes
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.OrderIndex)
                    .ToListAsync(),

                StrategicObjectives = await _db.StrategicObjectives
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync(),

                MainObjectives = await _db.MainObjectives
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.OrderIndex)
                    .ToListAsync(),

                // تحميل الأهداف الفرعية مع دعم الـ API الخارجي
                SubObjectives = await _db.SubObjectives
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// صفحة القيم المؤسسية
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> CoreValues()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                CoreValues = await _db.CoreValues
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.OrderIndex)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }
    }
}