using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Services.Tenant;

namespace OperationalPlanMS.Controllers
{
    /// <summary>
    /// لوحة الإدارة الرئيسية + التوافق مع الروابط القديمة
    /// تم نقل كل الوظائف إلى Controllers مستقلة:
    /// - UsersController
    /// - FiscalYearsController
    /// - RolesController
    /// - SupportingEntitiesController
    /// - StrategicPlanningController (includes VisionMission, Axes, Objectives, CoreValues, FinancialCosts)
    /// </summary>
    [Authorize]
    public class AdminController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;
        public AdminController(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        private bool IsAdminUser() => GetCurrentUserRole() == UserRole.Admin || GetCurrentUserRole() == UserRole.SuperAdmin;

        // الصفحة الرئيسية للإدارة
        public IActionResult Index()
        {
            if (!IsAdminUser()) return Forbid();
            return View();
        }

        // مزامنة البيانات الخارجية
        [HttpGet]
        public IActionResult ExternalSync()
        {
            if (!IsAdminUser()) return Forbid();
            ViewBag.ApiBaseUrl = _configuration["ExternalApi:BaseUrl"] ?? "غير محدد";
            ViewBag.ApiTenantId = _configuration["ExternalApi:TenantId"] ?? "1";
            return View();
        }

        // ========== Tenant Switcher — SuperAdmin فقط ==========

        /// <summary>
        /// GET /Admin/GetTenants — قائمة الوحدات الجذرية
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            if (!IsSuperAdmin()) return Forbid();

            var tenants = await _db.ExternalOrganizationalUnits
                .Where(u => u.ParentId == null && u.IsActive)
                .Select(u => new { u.Id, Name = u.ArabicName })
                .OrderBy(u => u.Name)
                .ToListAsync();

            var selectedId = HttpContext.Session.GetString(TenantProvider.SessionKey);

            return Json(new { tenants, selectedId = selectedId ?? "" });
        }

        /// <summary>
        /// POST /Admin/SwitchTenant — تبديل الوحدة المختارة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SwitchTenant(string? tenantId)
        {
            if (!IsSuperAdmin()) return Forbid();

            if (string.IsNullOrEmpty(tenantId))
            {
                // "جميع الوحدات"
                HttpContext.Session.Remove(TenantProvider.SessionKey);
            }
            else
            {
                HttpContext.Session.SetString(TenantProvider.SessionKey, tenantId);
            }

            // رجوع للصفحة السابقة
            var referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction("Index", "Home");
        }

        #region Backward Compatibility Redirects

        // Users
        public IActionResult Users() => RedirectToActionPermanent("Index", "Users");
        public IActionResult UserCreate() => RedirectToActionPermanent("Create", "Users");
        public IActionResult UserEdit(int id) => RedirectToActionPermanent("Edit", "Users", new { id });

        // FiscalYears
        public IActionResult FiscalYears() => RedirectToActionPermanent("Index", "FiscalYears");
        public IActionResult FiscalYearCreate() => RedirectToActionPermanent("Create", "FiscalYears");
        public IActionResult FiscalYearEdit(int id) => RedirectToActionPermanent("Edit", "FiscalYears", new { id });

        // Roles
        public IActionResult Roles() => RedirectToActionPermanent("Index", "Roles");
        public IActionResult RoleEdit(int id) => RedirectToActionPermanent("Edit", "Roles", new { id });

        // SupportingEntities
        public IActionResult SupportingEntities() => RedirectToActionPermanent("Index", "SupportingEntities");
        public IActionResult SupportingEntityCreate() => RedirectToActionPermanent("Create", "SupportingEntities");
        public IActionResult SupportingEntityEdit(int id) => RedirectToActionPermanent("Edit", "SupportingEntities", new { id });

        // Strategic Planning
        public IActionResult StrategicPlanning() => RedirectToActionPermanent("Index", "StrategicPlanning");
        public IActionResult VisionMission() => RedirectToActionPermanent("VisionMission", "StrategicPlanning");
        public IActionResult EditAxis(int id) => RedirectToActionPermanent("EditAxis", "StrategicPlanning", new { id });
        public IActionResult EditStrategicObjective(int id) => RedirectToActionPermanent("EditStrategicObjective", "StrategicPlanning", new { id });
        public IActionResult EditMainObjective(int id) => RedirectToActionPermanent("EditMainObjective", "StrategicPlanning", new { id });
        public IActionResult EditSubObjective(int id) => RedirectToActionPermanent("EditSubObjective", "StrategicPlanning", new { id });
        public IActionResult EditValue(int id) => RedirectToActionPermanent("EditValue", "StrategicPlanning", new { id });
        public IActionResult EditUnitSettings(int id) => RedirectToActionPermanent("EditUnitSettings", "StrategicPlanning", new { id });
        public IActionResult FinancialCosts() => RedirectToActionPermanent("FinancialCosts", "StrategicPlanning");
        public IActionResult EditFinancialCost(int id) => RedirectToActionPermanent("EditFinancialCost", "StrategicPlanning", new { id });

        #endregion


        /// <summary>
        /// GET /Admin/ChatbotStatus — يرجع حالة المساعد الذكي (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ChatbotStatus()
        {
            if (!IsAdminUser()) return Forbid();
            var settings = await _db.SystemSettings.FirstOrDefaultAsync();
            return Json(new { enabled = settings?.IsChatbotEnabled ?? true });
        }

        /// <summary>
        /// POST /Admin/ToggleChatbot — تفعيل/إيقاف المساعد الذكي
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleChatbot()
        {
            if (!IsAdminUser()) return Forbid();

            var settings = await _db.SystemSettings.FirstOrDefaultAsync();
            if (settings == null) return NotFound();

            settings.IsChatbotEnabled = !settings.IsChatbotEnabled;
            settings.LastModifiedById = GetCurrentUserId();
            settings.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { enabled = settings.IsChatbotEnabled });
        }
    }
}
