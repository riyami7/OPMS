using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class AuditController : Controller
    {
        private readonly IAuditService _auditService;
        private readonly AppDbContext _db;

        public AuditController(IAuditService auditService, AppDbContext db)
        {
            _auditService = auditService;
            _db = db;
        }

        public async Task<IActionResult> Index(string? entityType, string? actionFilter, int? userId,
      DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            // Admin و Executive فقط
            var roleStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            Enum.TryParse<UserRole>(roleStr, out var userRole);
            if (userRole != UserRole.Admin && userRole != UserRole.Executive)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية الوصول لسجل التدقيق";
                return RedirectToAction("Index", "Home");
            }
            const int pageSize = 30;
            var logs = await _auditService.GetLogsAsync(entityType, actionFilter, userId, fromDate, toDate, page, pageSize);
            var totalCount = await _auditService.GetTotalCountAsync(entityType, actionFilter, userId, fromDate, toDate);
            ViewBag.EntityType = entityType;
            ViewBag.Action = actionFilter;
            ViewBag.UserId = userId;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Users = new SelectList(
                await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FullNameAr)
                    .Select(u => new { u.Id, Name = u.FullNameAr }).ToListAsync(),
                "Id", "Name", userId);
            return View(logs);
        }

    }
}
