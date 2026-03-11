using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;

namespace OperationalPlanMS.Filters
{
    /// <summary>
    /// يحقن عدد الخطوات المعلقة في ViewBag لكل Request
    /// لعرض badge في Sidebar للمؤكدين
    /// </summary>
    public class PendingApprovalsFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;

        public PendingApprovalsFilter(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is Controller controller && context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isStepApproverClaim = context.HttpContext.User.FindFirst("IsStepApprover")?.Value;
                var isStepApprover = isStepApproverClaim == "True" || isStepApproverClaim == "true";

                if (isStepApprover)
                {
                    var count = await _db.Steps
                        .Where(s => s.ApprovalStatus == ApprovalStatus.Pending && !s.IsDeleted)
                        .CountAsync();
                    controller.ViewBag.PendingApprovalsCount = count;
                }
            }

            await next();
        }
    }
}
