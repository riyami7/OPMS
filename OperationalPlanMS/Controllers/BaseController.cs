using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OperationalPlanMS.Models;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;

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
        /// Check if current user is regular User (Project Manager)
        /// </summary>
        protected bool IsRegularUser() => GetCurrentUserRole() == UserRole.User;

        /// <summary>
        /// Check if current user is StepUser (step executor)
        /// </summary>
        protected bool IsStepUser() => GetCurrentUserRole() == UserRole.StepUser;

        /// <summary>
        /// Check if user can edit (Admin only) - kept for backward compatibility
        /// </summary>
        protected bool CanEdit() => IsAdmin();

        /// <summary>
        /// Check if user can view all (Admin or Executive)
        /// </summary>
        protected bool CanViewAll() => IsAdmin() || IsExecutive();

        /// <summary>
        /// Check if user can create/edit/delete initiatives (Admin or Supervisor)
        /// </summary>
        protected bool CanEditInitiatives() => IsAdmin() || IsSupervisor();

        /// <summary>
        /// Check if user can create/edit/delete projects (Admin or Supervisor)
        /// </summary>
        protected bool CanEditProjects() => IsAdmin() || IsSupervisor();

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

        /// <summary>
        /// Set shared ViewBag data for all pages (chatbot toggle, etc.)
        /// </summary>
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            // Load chatbot setting for _ChatWidget
            var db = HttpContext.RequestServices.GetService<AppDbContext>();
            if (db != null)
            {
                var settings = db.SystemSettings.AsNoTracking().FirstOrDefault();
                ViewBag.IsChatbotEnabled = settings?.IsChatbotEnabled ?? false;
            }
        }
    }
}