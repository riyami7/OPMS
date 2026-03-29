using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Services
{
    public interface INotificationService
    {
        Task CreateAsync(int userId, string title, string? message, string type, string? url, string icon = "bi-bell");
        Task CreateForMultipleAsync(IEnumerable<int> userIds, string title, string? message, string type, string? url, string icon = "bi-bell");
        Task<List<Notification>> GetUserNotificationsAsync(int userId, int count = 10);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;

        public NotificationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task CreateAsync(int userId, string title, string? message, string type, string? url, string icon = "bi-bell")
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                Icon = icon,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        public async Task CreateForMultipleAsync(IEnumerable<int> userIds, string title, string? message, string type, string? url, string icon = "bi-bell")
        {
            var notifications = userIds.Distinct().Select(uid => new Notification
            {
                UserId = uid,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                Icon = icon,
                CreatedAt = DateTime.Now
            }).ToList();

            if (notifications.Any())
            {
                _db.Notifications.AddRange(notifications);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int count = 10)
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.Now;
            }
            if (unread.Any()) await _db.SaveChangesAsync();
        }
    }
}
