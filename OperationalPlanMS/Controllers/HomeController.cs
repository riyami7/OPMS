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

                if (userRole == UserRole.Admin)
                    await LoadAdminDashboard(viewModel);
                else if (userRole == UserRole.Executive)
                    await LoadExecutiveDashboard(viewModel);
                else
                    await LoadMyDashboard(viewModel, userId);

                // الإشعارات الأخيرة — لكل المستخدمين
                viewModel.RecentNotifications = await _db.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync();

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

        private async Task LoadAdminDashboard(DashboardViewModel model)
        {
            var initiatives = await _db.Initiatives.Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted)).ToListAsync();
            var projects = await _db.Projects.Where(p => !p.IsDeleted)
                .Include(p => p.Initiative).Include(p => p.Steps.Where(s => !s.IsDeleted)).ToListAsync();

            model.TotalInitiatives = initiatives.Count;
            model.TotalProjects = projects.Count;
            model.TotalSteps = await _db.Steps.CountAsync(s => !s.IsDeleted);
            model.TotalUsers = await _db.Users.CountAsync(u => u.IsActive);

            model.CompletedInitiatives = initiatives.Count(i => i.Status == Status.Completed);
            model.InProgressInitiatives = initiatives.Count(i => i.Status == Status.InProgress);
            model.DelayedInitiatives = initiatives.Count(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);
            model.CompletedProjects = projects.Count(p => p.Status == Status.Completed);
            model.InProgressProjects = projects.Count(p => p.Status == Status.InProgress);
            model.DelayedProjects = projects.Count(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed);

            model.AverageInitiativeProgress = initiatives.Any() ? Math.Round(initiatives.Average(i => i.ProgressPercentage), 1) : 0;
            model.AverageProjectProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0;

            model.RecentInitiatives = initiatives.OrderByDescending(i => i.CreatedAt).Take(5).ToList();
            model.RecentProjects = projects.OrderByDescending(p => p.CreatedAt).Take(5).ToList();
            model.OverdueProjects = projects.Where(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed)
                .OrderBy(p => p.PlannedEndDate).Take(5).ToList();
        }

        private async Task LoadExecutiveDashboard(DashboardViewModel model)
        {
            await LoadAdminDashboard(model);
            model.TotalUsers = 0; // Executive ما يشوف عدد المستخدمين
        }

        /// <summary>
        /// لوحة تحكم شخصية — تعرض كل ما المستخدم معيّن عليه بغض النظر عن دوره
        /// </summary>
        private async Task LoadMyDashboard(DashboardViewModel model, int userId)
        {
            var empNumber = await _db.Users.Where(u => u.Id == userId)
                .Select(u => u.ADUsername).FirstOrDefaultAsync() ?? "";

            // === جمع كل المشاريع المرتبطة بالمستخدم ===

            // 1. مشاريع أنا مديرها
            var managedProjectIds = await _db.Projects
                .Where(p => !p.IsDeleted && p.ProjectManagerId == userId)
                .Select(p => p.Id).ToListAsync();

            // 2. مشاريع أنا مساعد مديرها
            var deputyProjectIds = !string.IsNullOrEmpty(empNumber)
                ? await _db.Projects.Where(p => !p.IsDeleted && p.DeputyManagerEmpNumber == empNumber)
                    .Select(p => p.Id).ToListAsync()
                : new List<int>();

            // 3. مشاريع فيها خطوات معيّنة لي
            var stepProjectIds = await _db.Steps
                .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                .Select(s => s.ProjectId).Distinct().ToListAsync();

            // 4. مبادرات أنا مشرفها
            var supervisedInitiativeIds = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.SupervisorId == userId)
                .Select(i => i.Id).ToListAsync();

            // 5. مبادرات عبر InitiativeAccess
            var accessInitiativeIds = await _db.InitiativeAccess
                .Where(a => a.UserId == userId && a.IsActive)
                .Select(a => a.InitiativeId).ToListAsync();

            // === تجميع كل الـ IDs ===
            var allMyProjectIds = managedProjectIds
                .Union(deputyProjectIds)
                .Union(stepProjectIds)
                .Distinct().ToList();

            var allMyInitiativeIds = supervisedInitiativeIds
                .Union(accessInitiativeIds)
                .Distinct().ToList();

            // أضف مبادرات المشاريع
            var projectInitiativeIds = await _db.Projects
                .Where(p => allMyProjectIds.Contains(p.Id))
                .Select(p => p.InitiativeId).Distinct().ToListAsync();
            allMyInitiativeIds = allMyInitiativeIds.Union(projectInitiativeIds).Distinct().ToList();

            // === تحميل البيانات ===
            var myInitiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted && allMyInitiativeIds.Contains(i.Id))
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var myProjects = await _db.Projects
                .Where(p => !p.IsDeleted && allMyProjectIds.Contains(p.Id))
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var mySteps = await _db.Steps
                .Where(s => !s.IsDeleted && s.AssignedToId == userId)
                .Include(s => s.Project)
                .OrderBy(s => s.PlannedEndDate)
                .ToListAsync();

            // === الإحصائيات ===
            model.TotalInitiatives = myInitiatives.Count;
            model.TotalProjects = myProjects.Count;
            model.TotalSteps = mySteps.Count;

            model.CompletedInitiatives = myInitiatives.Count(i => i.Status == Status.Completed);
            model.InProgressInitiatives = myInitiatives.Count(i => i.Status == Status.InProgress);
            model.DelayedInitiatives = myInitiatives.Count(i => i.PlannedEndDate < DateTime.Today && i.Status != Status.Completed);

            model.CompletedProjects = myProjects.Count(p => p.Status == Status.Completed);
            model.InProgressProjects = myProjects.Count(p => p.Status == Status.InProgress);
            model.DelayedProjects = myProjects.Count(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed);

            model.CompletedSteps = mySteps.Count(s => s.Status == StepStatus.Completed);
            model.InProgressSteps = mySteps.Count(s => s.Status == StepStatus.InProgress);
            model.DelayedSteps = mySteps.Count(s => s.PlannedEndDate < DateTime.Today && s.Status != StepStatus.Completed);

            model.AverageInitiativeProgress = myInitiatives.Any()
                ? Math.Round(myInitiatives.Average(i => i.ProgressPercentage), 1) : 0;
            model.AverageProjectProgress = myProjects.Any()
                ? Math.Round(myProjects.Average(p => p.ProgressPercentage), 1) : 0;

            // === القوائم ===
            model.RecentInitiatives = myInitiatives.Take(5).ToList();
            model.RecentProjects = myProjects.Take(5).ToList();
            model.MySteps = mySteps.Where(s => s.Status != StepStatus.Completed).Take(10).ToList();
            model.OverdueProjects = myProjects
                .Where(p => p.PlannedEndDate < DateTime.Today && p.Status != Status.Completed)
                .OrderBy(p => p.PlannedEndDate).Take(5).ToList();

            // خطوات متأخرة
            model.OverdueSteps = mySteps
                .Where(s => s.PlannedEndDate < DateTime.Today && s.Status != StepStatus.Completed)
                .OrderBy(s => s.PlannedEndDate).Take(5).ToList();

            // مواعيد قادمة (خطوات خلال 7 أيام)
            model.MyUpcomingDeadlines = mySteps
                .Where(s => s.Status != StepStatus.Completed
                    && s.PlannedEndDate >= DateTime.Today
                    && s.PlannedEndDate <= DateTime.Today.AddDays(7))
                .OrderBy(s => s.PlannedEndDate).Take(5).ToList();
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
                UnitSettings = await _db.OrganizationalUnitSettings.ToListAsync(),
                Axes = await _db.StrategicAxes.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToListAsync(),
                StrategicObjectives = await _db.StrategicObjectives.Where(s => s.IsActive).OrderBy(s => s.OrderIndex).ToListAsync(),
                MainObjectives = await _db.MainObjectives.Where(m => m.IsActive).OrderBy(m => m.OrderIndex).ToListAsync(),
                SubObjectives = await _db.SubObjectives.Where(s => s.IsActive).OrderBy(s => s.OrderIndex).ToListAsync(),
                CoreValues = await _db.CoreValues.Where(v => v.IsActive).OrderBy(v => v.OrderIndex).ToListAsync()
            };
            return View(viewModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> VisionMission()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                SystemSettings = await _db.SystemSettings.FirstOrDefaultAsync(),
                UnitSettings = await _db.OrganizationalUnitSettings.ToListAsync()
            };
            return View(viewModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> StrategicObjectives()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                Axes = await _db.StrategicAxes.Where(a => a.IsActive).OrderBy(a => a.OrderIndex).ToListAsync(),
                StrategicObjectives = await _db.StrategicObjectives.Where(s => s.IsActive).OrderBy(s => s.OrderIndex).ToListAsync(),
                MainObjectives = await _db.MainObjectives.Where(m => m.IsActive).OrderBy(m => m.OrderIndex).ToListAsync(),
                SubObjectives = await _db.SubObjectives.Where(s => s.IsActive).OrderBy(s => s.OrderIndex).ToListAsync()
            };
            return View(viewModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> CoreValues()
        {
            var viewModel = new StrategicOverviewViewModel
            {
                CoreValues = await _db.CoreValues.Where(v => v.IsActive).OrderBy(v => v.OrderIndex).ToListAsync()
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
