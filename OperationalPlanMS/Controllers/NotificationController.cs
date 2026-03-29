using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationalPlanMS.Services;
using System.Security.Claims;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private int GetCurrentUserId()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(idStr, out int userId);
            return userId;
        }

        // GET: /Notification — صفحة كاملة
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 50);
            return View(notifications);
        }

        // GET: /Notification/GetDropdown — للـ AJAX dropdown
        [HttpGet]
        public async Task<IActionResult> GetDropdown()
        {
            var userId = GetCurrentUserId();
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { notifications = notifications.Select(n => new {
                n.Id, n.Title, n.Message, n.Icon, n.Url, n.IsRead, n.TimeAgo, n.TypeBadgeClass
            }), unreadCount });
        }

        // POST: /Notification/MarkAsRead/5
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id, GetCurrentUserId());
            return Ok();
        }

        // POST: /Notification/MarkAllAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync(GetCurrentUserId());
            return Ok();
        }
    }
}
