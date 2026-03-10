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

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // Get current user info from claims
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            var userRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;
            var roleNameAr = User.FindFirst("RoleNameAr")?.Value;
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            int.TryParse(userIdStr, out int userId);
            Enum.TryParse<UserRole>(userRoleStr, out UserRole userRole);

            ViewBag.UserName = userName;
            ViewBag.UserRole = userRole;
            ViewBag.RoleNameAr = roleNameAr;

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
                    default:
                        await LoadBasicDashboard(viewModel);
                        break;
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ViewBag.DatabaseError = ex.Message;
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
            // إحصائيات شاملة
            model.TotalInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted);
            model.TotalProjects = await _db.Projects.CountAsync(p => !p.IsDeleted);
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);
            model.TotalUsers = await _db.Users.CountAsync(u => u.IsActive);

            // إحصائيات الحالة
            model.CompletedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.Status == Status.Completed);
            model.InProgressInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.Status == Status.InProgress);
            model.DelayedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.Status == Status.Completed);
            model.InProgressProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.Status == Status.InProgress);

            // متوسط الإنجاز
            var initiatives = await _db.Initiatives.Where(i => !i.IsDeleted).ToListAsync();
            model.AverageInitiativeProgress = initiatives.Any() ? Math.Round(initiatives.Average(i => i.ProgressPercentage), 1) : 0;

            var projects = await _db.Projects.Where(p => !p.IsDeleted).ToListAsync();
            model.AverageProjectProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            // آخر المبادرات
            model.RecentInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToListAsync();

            // آخر المشاريع
            model.RecentProjects = await _db.Projects
                .Where(p => !p.IsDeleted)
                .Include(p => p.Initiative)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // المبادرات المتأخرة
            model.OverdueInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .Include(i => i.Supervisor)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToListAsync();
        }

        private async Task LoadExecutiveDashboard(DashboardViewModel model)
        {
            model.TotalInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted);
            model.TotalProjects = await _db.Projects.CountAsync(p => !p.IsDeleted);
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);

            model.CompletedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.Status == Status.Completed);
            model.InProgressInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.Status == Status.InProgress);
            model.DelayedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.Status == Status.Completed);
            model.InProgressProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.Status == Status.InProgress);

            var initiatives = await _db.Initiatives.Where(i => !i.IsDeleted).ToListAsync();
            model.AverageInitiativeProgress = initiatives.Any() ? Math.Round(initiatives.Average(i => i.ProgressPercentage), 1) : 0;

            var projects = await _db.Projects.Where(p => !p.IsDeleted).ToListAsync();
            model.AverageProjectProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            model.RecentInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Supervisor)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToListAsync();

            model.OverdueInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .Include(i => i.Supervisor)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToListAsync();
        }

        private async Task LoadSupervisorDashboard(DashboardViewModel model, int userId)
        {
            // المبادرات المعين عليها
            var myInitiativeIds = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.SupervisorId == userId)
                .Select(i => i.Id)
                .ToListAsync();

            model.TotalInitiatives = myInitiativeIds.Count;
            model.TotalProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId));

            var myProjectIds = await _db.Projects
                .Where(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId))
                .Select(p => p.Id)
                .ToListAsync();
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted && myProjectIds.Contains(s.ProjectId));

            model.CompletedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.SupervisorId == userId && i.Status == Status.Completed);
            model.InProgressInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.SupervisorId == userId && i.Status == Status.InProgress);
            model.DelayedInitiatives = await _db.Initiatives.CountAsync(i => !i.IsDeleted && i.SupervisorId == userId && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId) && p.Status == Status.Completed);
            model.InProgressProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId) && p.Status == Status.InProgress);

            if (myInitiativeIds.Any())
            {
                var myInitiatives = await _db.Initiatives.Where(i => myInitiativeIds.Contains(i.Id)).ToListAsync();
                model.AverageInitiativeProgress = Math.Round(myInitiatives.Average(i => i.ProgressPercentage), 1);
            }

            model.RecentInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.SupervisorId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToListAsync();

            model.RecentProjects = await _db.Projects
                .Where(p => !p.IsDeleted && myInitiativeIds.Contains(p.InitiativeId))
                .Include(p => p.Initiative)
                .Include(p => p.ProjectManager)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            model.OverdueInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.SupervisorId == userId && i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed)
                .OrderBy(i => i.PlannedEndDate)
                .Take(5)
                .ToListAsync();
        }

        private async Task LoadUserDashboard(DashboardViewModel model, int userId)
        {
            // المشاريع المعين عليها
            var myProjectIds = await _db.Projects
                .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            model.TotalProjects = myProjectIds.Count;
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted && myProjectIds.Contains(s.ProjectId));

            model.CompletedProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.ProjectManagerId == userId && p.Status == Status.Completed);
            model.InProgressProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.ProjectManagerId == userId && p.Status == Status.InProgress);
            model.DelayedProjects = await _db.Projects.CountAsync(p => !p.IsDeleted && p.ProjectManagerId == userId && p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed);

            model.CompletedSteps = await _db.Steps.CountAsync(s => !s.IsDeleted && myProjectIds.Contains(s.ProjectId) && s.Status == StepStatus.Completed);
            model.InProgressSteps = await _db.Steps.CountAsync(s => !s.IsDeleted && myProjectIds.Contains(s.ProjectId) && s.Status == StepStatus.InProgress);

            if (myProjectIds.Any())
            {
                var myProjects = await _db.Projects.Where(p => myProjectIds.Contains(p.Id)).ToListAsync();
                model.AverageProjectProgress = Math.Round(myProjects.Average(p => p.ProgressPercentage), 1);
            }

            model.RecentProjects = await _db.Projects
                .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                .Include(p => p.Initiative)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            model.MySteps = await _db.Steps
                .Where(s => !s.IsDeleted && (s.AssignedToId == userId || myProjectIds.Contains(s.ProjectId)))
                .Include(s => s.Project)
                .OrderBy(s => s.PlannedEndDate)
                .Take(10)
                .ToListAsync();

            model.OverdueProjects = await _db.Projects
                .Where(p => !p.IsDeleted && p.ProjectManagerId == userId && p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed)
                .Include(p => p.Initiative)
                .OrderBy(p => p.PlannedEndDate)
                .Take(5)
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