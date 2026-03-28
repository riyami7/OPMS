using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Services;

namespace OperationalPlanMS.Controllers.Api
{
    /// <summary>
    /// API للوحدات التنظيمية والموظفين
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [IgnoreAntiforgeryToken]    // أضف هذا السطر
    [EnableRateLimiting("api")]
    public class OrganizationApiController : ControllerBase
    {
        private readonly IExternalApiService _externalApiService;
        private readonly AppDbContext _db;
        private readonly ILogger<OrganizationApiController> _logger;

        public OrganizationApiController(
            IExternalApiService externalApiService,
            AppDbContext db,
            ILogger<OrganizationApiController> logger)
        {
            _externalApiService = externalApiService;
            _db = db;
            _logger = logger;
        }

        #region Organizational Units

        /// <summary>
        /// جلب جميع الوحدات التنظيمية من الجدول المحلي
        /// </summary>
        [HttpGet("units/all")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetAllUnits()
        {
            try
            {
                var units = await _db.ExternalOrganizationalUnits
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.ArabicName)
                    .Select(u => new
                    {
                        id = u.Id,
                        parentId = u.ParentId,
                        name = u.ArabicName ?? u.ArabicUnitName ?? "",
                        code = u.Code
                    })
                    .ToListAsync();

                return Ok(units);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizational units");
                return StatusCode(500, new { error = "حدث خطأ أثناء جلب الوحدات التنظيمية" });
            }
        }

        /// <summary>
        /// جلب الوحدات الجذر (المستوى الأول)
        /// </summary>
        [HttpGet("units/root")]
        public async Task<IActionResult> GetRootUnits()
        {
            try
            {
                var units = await _db.ExternalOrganizationalUnits
                    .Where(u => u.IsActive && (u.ParentId == null))
                    .OrderBy(u => u.ArabicName)
                    .Select(u => new
                    {
                        id = u.Id,
                        name = u.ArabicName ?? u.ArabicUnitName ?? "",
                        code = u.Code
                    })
                    .ToListAsync();

                return Ok(units);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting root units");
                return StatusCode(500, new { error = "حدث خطأ" });
            }
        }

        /// <summary>
        /// جلب الوحدات الفرعية لوحدة معينة
        /// </summary>
        [HttpGet("units/{parentId}/children")]
        public async Task<IActionResult> GetChildUnits(Guid parentId)
        {
            try
            {
                var units = await _db.ExternalOrganizationalUnits
                    .Where(u => u.IsActive && u.ParentId == parentId)
                    .OrderBy(u => u.ArabicName)
                    .Select(u => new
                    {
                        id = u.Id,
                        parentId = u.ParentId,
                        name = u.ArabicName ?? u.ArabicUnitName ?? "",
                        code = u.Code
                    })
                    .ToListAsync();

                return Ok(units);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting child units for parent {ParentId}", parentId);
                return StatusCode(500, new { error = "حدث خطأ" });
            }
        }

        /// <summary>
        /// مزامنة الوحدات التنظيمية من API الخارجي
        /// </summary>
        [HttpPost("units/sync")]
        public async Task<IActionResult> SyncUnits()
        {
            try
            {
                var result = await _externalApiService.SyncOrganizationalUnitsAsync();

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = $"تمت المزامنة بنجاح: {result.AddedCount} جديد، {result.UpdatedCount} محدّث",
                        addedCount = result.AddedCount,
                        updatedCount = result.UpdatedCount,
                        totalCount = result.TotalCount,
                        syncedAt = result.SyncedAt
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.ErrorMessage ?? "فشلت المزامنة"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing organizational units");
                return StatusCode(500, new { success = false, error = "حدث خطأ أثناء المزامنة. يرجى المحاولة لاحقاً." });
            }
        }

        /// <summary>
        /// الحصول على آخر تاريخ مزامنة
        /// </summary>
        [HttpGet("units/sync-status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            try
            {
                var lastSync = await _db.ExternalOrganizationalUnits
                    .OrderByDescending(u => u.LastSyncAt)
                    .Select(u => u.LastSyncAt)
                    .FirstOrDefaultAsync();

                var count = await _db.ExternalOrganizationalUnits.CountAsync(u => u.IsActive);

                return Ok(new
                {
                    lastSyncAt = lastSync,
                    totalCount = count,
                    hasData = count > 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync status");
                return StatusCode(500, new { error = "حدث خطأ" });
            }
        }

        #endregion

        #region Employees

        /// <summary>
        /// البحث عن الموظفين
        /// </summary>
        [HttpGet("employees/search")]
        public async Task<IActionResult> SearchEmployees([FromQuery] string? term)
        {
            try
            {
                var employees = await _externalApiService.SearchEmployeesAsync(term ?? "");

                return Ok(employees.Select(e => new
                {
                    empNumber = e.EmpNumber,
                    name = e.Name,
                    rank = e.Rank,
                    position = e.Position,
                    unit = e.Unit
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching employees");
                return StatusCode(500, new { error = "حدث خطأ أثناء البحث" });
            }
        }

        /// <summary>
        /// جلب موظف برقم الموظف بالضبط
        /// </summary>
        [HttpGet("employees/by-number")]
        public async Task<IActionResult> GetEmployeeByNumber([FromQuery] string empNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empNumber))
                {
                    return BadRequest(new { error = "رقم الموظف مطلوب" });
                }

                var employee = await _externalApiService.GetEmployeeByNumberAsync(empNumber);

                if (employee == null)
                {
                    return NotFound(new { error = "لم يتم العثور على موظف بهذا الرقم" });
                }

                return Ok(new
                {
                    empNumber = employee.EmpNumber,
                    name = employee.Name,
                    rank = employee.Rank,
                    position = employee.Position,
                    unit = employee.Unit,
                    displayName = employee.DisplayName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee by number: {EmpNumber}", empNumber);
                return StatusCode(500, new { error = "حدث خطأ أثناء البحث" });
            }
        }

        /// <summary>
        /// التحقق من وجود موظف برقم معين
        /// </summary>
        [HttpGet("employees/exists")]
        public async Task<IActionResult> CheckEmployeeExists([FromQuery] string empNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empNumber))
                {
                    return BadRequest(new { exists = false, error = "رقم الموظف مطلوب" });
                }

                var employee = await _externalApiService.GetEmployeeByNumberAsync(empNumber);

                // التحقق من عدم وجود مستخدم بنفس الرقم في النظام
                var userExists = await _db.Users.AnyAsync(u => u.ADUsername == empNumber);

                return Ok(new
                {
                    exists = employee != null,
                    alreadyRegistered = userExists,
                    employee = employee != null ? new
                    {
                        empNumber = employee.EmpNumber,
                        name = employee.Name,
                        rank = employee.Rank
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking employee existence: {EmpNumber}", empNumber);
                return StatusCode(500, new { error = "حدث خطأ" });
            }
        }

        #endregion

        #region Cache

        /// <summary>
        /// مسح الـ Cache
        /// </summary>
        [HttpPost("cache/clear")]
        public IActionResult ClearCache()
        {
            _externalApiService.ClearCache();
            return Ok(new { message = "تم مسح الـ Cache بنجاح" });
        }

        #endregion
    }
}