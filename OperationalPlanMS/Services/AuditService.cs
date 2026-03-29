using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Services
{
    public interface IAuditService
    {
        Task LogAsync(string entityType, int entityId, string? entityName, string action, int userId, string? details = null, string? oldValue = null, string? newValue = null, string? ipAddress = null);
        Task<List<AuditLog>> GetLogsAsync(string? entityType = null, string? action = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50);
        Task<int> GetTotalCountAsync(string? entityType = null, string? action = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _db;

        public AuditService(AppDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(string entityType, int entityId, string? entityName, string action, int userId, string? details = null, string? oldValue = null, string? newValue = null, string? ipAddress = null)
        {
            var log = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Action = action,
                Details = details,
                OldValue = oldValue,
                NewValue = newValue,
                UserId = userId,
                IpAddress = ipAddress,
                CreatedAt = DateTime.Now
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        public async Task<List<AuditLog>> GetLogsAsync(string? entityType = null, string? action = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50)
        {
            var query = _db.AuditLogs.Include(a => a.User).AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action == action);
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);
            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt < toDate.Value.AddDays(1));

            return await query.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? entityType = null, string? action = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _db.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action == action);
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);
            if (fromDate.HasValue)
                query = query.Where(a => a.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.CreatedAt < toDate.Value.AddDays(1));

            return await query.CountAsync();
        }
    }
}
