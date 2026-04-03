namespace OperationalPlanMS.Services.Tenant
{
    /// <summary>
    /// يوفر TenantId الحالي من الجلسة
    /// SuperAdmin = TenantId is null (يشوف الكل)
    /// </summary>
    public interface ITenantProvider
    {
        /// <summary>
        /// معرف الـ Tenant الحالي — null يعني SuperAdmin
        /// </summary>
        Guid? CurrentTenantId { get; }

        /// <summary>
        /// هل المستخدم الحالي SuperAdmin (يتجاوز فلتر الـ tenant)
        /// </summary>
        bool IsSuperAdmin { get; }
    }
}
