using System.Security.Claims;
using OperationalPlanMS.Models;

namespace OperationalPlanMS.Services.Tenant
{
    /// <summary>
    /// يقرأ TenantId من Claims + Session (للـ SuperAdmin)
    /// SuperAdmin يقدر يتنقل بين الـ tenants عبر Session
    /// </summary>
    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// مفتاح Session لتخزين الـ Tenant المختار من SuperAdmin
        /// </summary>
        public const string SessionKey = "SelectedTenantId";

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? CurrentTenantId
        {
            get
            {
                // SuperAdmin: يتحقق من Session أولاً
                if (IsSuperAdmin)
                {
                    var selectedTenant = _httpContextAccessor.HttpContext?.Session?.GetString(SessionKey);
                    if (!string.IsNullOrEmpty(selectedTenant) && Guid.TryParse(selectedTenant, out var selectedId))
                        return selectedId; // يشوف وحدة محددة

                    return null; // يشوف الكل
                }

                // باقي المستخدمين: TenantId من Claims
                var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tenantId))
                    return tenantId;

                return null;
            }
        }

        public bool IsSuperAdmin
        {
            get
            {
                var roleClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
                return roleClaim == UserRole.SuperAdmin.ToString();
            }
        }
    }
}
