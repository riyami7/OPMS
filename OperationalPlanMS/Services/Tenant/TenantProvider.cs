using System.Security.Claims;
using OperationalPlanMS.Models;

namespace OperationalPlanMS.Services.Tenant
{
    /// <summary>
    /// يقرأ TenantId من Claims الحالية
    /// </summary>
    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? CurrentTenantId
        {
            get
            {
                if (IsSuperAdmin) return null;

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
