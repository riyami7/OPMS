using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;

namespace OperationalPlanMS.Filters
{
    /// <summary>
    /// Global filter that sets ViewBag.IsChatbotEnabled for ALL controllers.
    /// Registered in Program.cs so it runs on every request regardless of base class.
    /// </summary>
    public class ChatbotSettingsFilter : IActionFilter
    {
        private readonly AppDbContext _db;

        public ChatbotSettingsFilter(AppDbContext db)
        {
            _db = db;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is Controller controller)
            {
                var settings = _db.SystemSettings.AsNoTracking().FirstOrDefault();
                controller.ViewBag.IsChatbotEnabled = settings?.IsChatbotEnabled ?? false;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Nothing needed
        }
    }
}
