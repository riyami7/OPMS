using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OperationalPlanMS.Models;

namespace OperationalPlanMS.Controllers
{
    /// <summary>
    /// Base controller with common authorization methods
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Get current user ID from claims
        /// </summary>
        protected int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        /// <summary>
        /// Get current user role from claims
        /// </summary>
        protected UserRole GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (Enum.TryParse<UserRole>(roleClaim, out UserRole role))
            {
                return role;
            }
            return UserRole.User; // Default to least privileged
        }

        /// <summary>
        /// Check if current user is Admin
        /// </summary>
        protected bool IsAdmin() => GetCurrentUserRole() == UserRole.Admin;

        /// <summary>
        /// Check if current user is Executive
        /// </summary>
        protected bool IsExecutive() => GetCurrentUserRole() == UserRole.Executive;

        /// <summary>
        /// Check if current user is Supervisor
        /// </summary>
        protected bool IsSupervisor() => GetCurrentUserRole() == UserRole.Supervisor;

        /// <summary>
        /// Check if current user is regular User
        /// </summary>
        protected bool IsRegularUser() => GetCurrentUserRole() == UserRole.User;

        /// <summary>
        /// Check if user can edit (Admin only)
        /// </summary>
        protected bool CanEdit() => IsAdmin();

        /// <summary>
        /// Check if user can view all (Admin or Executive)
        /// </summary>
        protected bool CanViewAll() => IsAdmin() || IsExecutive();

        /// <summary>
        /// Get Arabic name for Status
        /// </summary>
        protected string GetStatusArabicName(Status status) => status switch
        {
            Status.Draft => "مسودة",
            Status.Pending => "قيد الانتظار",
            Status.Approved => "معتمد",
            Status.InProgress => "قيد التنفيذ",
            Status.OnHold => "متوقف",
            Status.Completed => "مكتمل",
            Status.Cancelled => "ملغي",
            Status.Delayed => "متأخر",
            _ => "غير محدد"
        };

        /// <summary>
        /// Get Arabic name for Priority
        /// </summary>
        protected string GetPriorityArabicName(Priority priority) => priority switch
        {
            Priority.Highest => "الأعلى",
            Priority.High => "عالي",
            Priority.Medium => "متوسط",
            Priority.Low => "منخفض",
            Priority.Lowest => "الأدنى",
            _ => "غير محدد"
        };

        /// <summary>
        /// Get Arabic name for StepStatus
        /// </summary>
        protected string GetStepStatusArabicName(StepStatus status) => status switch
        {
            StepStatus.NotStarted => "لم يبدأ",
            StepStatus.InProgress => "قيد التنفيذ",
            StepStatus.Completed => "مكتمل",
            StepStatus.OnHold => "متوقف",
            StepStatus.Cancelled => "ملغي",
            _ => "غير محدد"
        };
    }
}
