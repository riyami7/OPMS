using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Filters;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Api;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Controllers
{
    /// <summary>
    /// Read-only Data API for external system integration (AI chatbot, BI tools, etc.)
    /// Secured via X-API-Key header — does NOT use cookie authentication.
    /// All endpoints return DTOs (no circular refs, no sensitive data, enums as strings).
    /// </summary>
    [Route("api/data")]
    [ApiController]
    [AllowAnonymous]
    [ApiKeyAuth]
    [EnableRateLimiting("api")]
    public class DataApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DataApiController(AppDbContext db)
        {
            _db = db;
        }

        // ================================================================
        //  Dashboard Summary
        // ================================================================

        /// <summary>
        /// GET /api/data/summary — overall system statistics
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(int? fiscalYearId)
        {
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Where(i => !fiscalYearId.HasValue || i.FiscalYearId == fiscalYearId)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .ToListAsync();

            var projects = initiatives.SelectMany(i => i.Projects).ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            var currentFy = await _db.FiscalYears.FirstOrDefaultAsync(f => f.IsCurrent);

            var dto = new DashboardSummaryDto
            {
                TotalInitiatives = initiatives.Count,
                TotalProjects = projects.Count,
                TotalSteps = steps.Count,
                CompletedInitiatives = initiatives.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100),
                CompletedSteps = steps.Count(s => s.ProgressPercentage >= 100),
                DelayedProjects = projects.Count(p => IsDelayed(p)),
                DelayedSteps = steps.Count(s => IsStepDelayed(s)),
                AverageProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0,
                TotalBudget = initiatives.Sum(i => i.Budget ?? 0) + projects.Sum(p => p.Budget ?? 0),
                TotalActualCost = initiatives.Sum(i => i.ActualCost ?? 0) + projects.Sum(p => p.ActualCost ?? 0),
                CurrentFiscalYear = currentFy == null ? null : MapFiscalYear(currentFy)
            };

            return Ok(new ApiResponse<DashboardSummaryDto> { Data = dto });
        }

        // ================================================================
        //  Fiscal Years
        // ================================================================

        /// <summary>
        /// GET /api/data/fiscal-years
        /// </summary>
        [HttpGet("fiscal-years")]
        public async Task<IActionResult> GetFiscalYears()
        {
            var items = await _db.FiscalYears.OrderByDescending(f => f.Year).ToListAsync();
            return Ok(new ApiResponse<List<FiscalYearDto>>
            {
                Data = items.Select(MapFiscalYear).ToList(),
                TotalCount = items.Count
            });
        }

        // ================================================================
        //  Initiatives
        // ================================================================

        /// <summary>
        /// GET /api/data/initiatives?fiscalYearId=1&unitId=5&search=keyword
        /// </summary>
        [HttpGet("initiatives")]
        public async Task<IActionResult> GetInitiatives(int? fiscalYearId, Guid? unitId, string? search)
        {
            var query = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Supervisor)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .AsQueryable();

            if (fiscalYearId.HasValue)
                query = query.Where(i => i.FiscalYearId == fiscalYearId);
            if (unitId.HasValue)
                query = query.Where(i => i.ExternalUnitId == unitId);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(i => i.NameAr.Contains(search) || i.Code.Contains(search));

            var items = await query.OrderBy(i => i.Code).ToListAsync();

            return Ok(new ApiResponse<List<InitiativeListDto>>
            {
                Data = items.Select(MapInitiativeList).ToList(),
                TotalCount = items.Count
            });
        }

        /// <summary>
        /// GET /api/data/initiatives/5
        /// </summary>
        [HttpGet("initiatives/{id}")]
        public async Task<IActionResult> GetInitiative(int id)
        {
            var item = await _db.Initiatives
                .Where(i => !i.IsDeleted && i.Id == id)
                .Include(i => i.Supervisor)
                .Include(i => i.FiscalYear)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Initiative not found" });

            var dto = new InitiativeDetailDto
            {
                Id = item.Id,
                Code = item.Code,
                NameAr = item.NameAr,
                NameEn = item.NameEn,
                DescriptionAr = item.DescriptionAr,
                DescriptionEn = item.DescriptionEn,
                UnitName = item.ExternalUnitName,
                SupervisorName = item.SupervisorName ?? item.Supervisor?.FullNameAr,
                StrategicObjective = item.StrategicObjective,
                FiscalYearId = item.FiscalYearId,
                FiscalYearName = item.FiscalYear?.NameAr,
                Budget = item.Budget,
                ActualCost = item.ActualCost,
                PlannedStartDate = item.PlannedStartDate,
                PlannedEndDate = item.PlannedEndDate,
                ActualStartDate = item.ActualStartDate,
                ActualEndDate = item.ActualEndDate,
                ProgressPercentage = item.Projects.Any()
                    ? Math.Round(item.Projects.Average(p => p.ProgressPercentage), 1) : 0,
                ProjectCount = item.Projects.Count,
                CompletedProjectCount = item.Projects.Count(p => p.ProgressPercentage >= 100),
                Status = GetInitiativeStatus(item),
                IsDelayed = item.Projects.Any(p => IsDelayed(p)),
                Projects = item.Projects.Select(MapProjectList).ToList()
            };

            return Ok(new ApiResponse<InitiativeDetailDto> { Data = dto });
        }

        // ================================================================
        //  Projects
        // ================================================================

        /// <summary>
        /// GET /api/data/projects?initiativeId=1&search=keyword
        /// </summary>
        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects(int? initiativeId, Guid? unitId, string? search)
        {
            var query = _db.Projects
                .Where(p => !p.IsDeleted)
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .AsQueryable();

            if (initiativeId.HasValue)
                query = query.Where(p => p.InitiativeId == initiativeId);
            if (unitId.HasValue)
                query = query.Where(p => p.Initiative.ExternalUnitId == unitId);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.NameAr.Contains(search) || p.Code.Contains(search));

            var items = await query.OrderBy(p => p.Code).ToListAsync();

            return Ok(new ApiResponse<List<ProjectListDto>>
            {
                Data = items.Select(MapProjectList).ToList(),
                TotalCount = items.Count
            });
        }

        /// <summary>
        /// GET /api/data/projects/5
        /// </summary>
        [HttpGet("projects/{id}")]
        public async Task<IActionResult> GetProject(int id)
        {
            var item = await _db.Projects
                .Where(p => !p.IsDeleted && p.Id == id)
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                    .ThenInclude(s => s.AssignedTo)
                .Include(p => p.Requirements)
                .Include(p => p.ProjectKPIs)
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Project not found" });

            var dto = new ProjectDetailDto
            {
                Id = item.Id,
                Code = item.Code,
                NameAr = item.NameAr,
                NameEn = item.NameEn,
                DescriptionAr = item.DescriptionAr,
                DescriptionEn = item.DescriptionEn,
                OperationalGoal = item.OperationalGoal,
                ExpectedOutcomes = item.ExpectedOutcomes,
                RiskNotes = item.RiskNotes,
                ProjectManagerName = item.ProjectManagerName,
                UnitName = item.ExternalUnitName,
                Budget = item.Budget,
                ActualCost = item.ActualCost,
                PlannedStartDate = item.PlannedStartDate,
                PlannedEndDate = item.PlannedEndDate,
                ActualStartDate = item.ActualStartDate,
                ActualEndDate = item.ActualEndDate,
                ProgressPercentage = item.ProgressPercentage,
                StepCount = item.Steps.Count,
                CompletedStepCount = item.Steps.Count(s => s.ProgressPercentage >= 100),
                Status = GetProjectStatus(item),
                IsDelayed = IsDelayed(item),
                InitiativeId = item.InitiativeId,
                InitiativeName = item.Initiative?.NameAr,
                Steps = item.Steps.OrderBy(s => s.StepNumber).Select(MapStepList).ToList(),
                Requirements = item.Requirements?.OrderBy(r => r.OrderIndex).Select(r => r.RequirementText).ToList() ?? new(),
                KPIs = item.ProjectKPIs?.OrderBy(k => k.OrderIndex).Select(k => new KpiDto
                {
                    KpiText = k.KPIText,
                    TargetValue = k.TargetValue,
                    ActualValue = k.ActualValue
                }).ToList() ?? new()
            };

            return Ok(new ApiResponse<ProjectDetailDto> { Data = dto });
        }

        // ================================================================
        //  Steps
        // ================================================================

        /// <summary>
        /// GET /api/data/steps?projectId=1&status=delayed
        /// </summary>
        [HttpGet("steps")]
        public async Task<IActionResult> GetSteps(int? projectId, string? status)
        {
            var query = _db.Steps
                .Where(s => !s.IsDeleted)
                .Include(s => s.Project)
                .Include(s => s.AssignedTo)
                .AsQueryable();

            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId);

            var items = await query.OrderBy(s => s.ProjectId).ThenBy(s => s.StepNumber).ToListAsync();

            // Filter by calculated status in memory
            if (!string.IsNullOrEmpty(status))
            {
                items = items.Where(s => GetStepStatus(s).Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(new ApiResponse<List<StepListDto>>
            {
                Data = items.Select(MapStepList).ToList(),
                TotalCount = items.Count
            });
        }

        /// <summary>
        /// GET /api/data/steps/5
        /// </summary>
        [HttpGet("steps/{id}")]
        public async Task<IActionResult> GetStep(int id)
        {
            var item = await _db.Steps
                .Where(s => !s.IsDeleted && s.Id == id)
                .Include(s => s.Project)
                .Include(s => s.AssignedTo)
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Step not found" });

            var dto = new StepDetailDto
            {
                Id = item.Id,
                StepNumber = item.StepNumber,
                NameAr = item.NameAr,
                NameEn = item.NameEn,
                DescriptionAr = item.DescriptionAr,
                DescriptionEn = item.DescriptionEn,
                AssignedToName = item.AssignedToName ?? item.AssignedTo?.FullNameAr,
                Weight = item.Weight,
                ProgressPercentage = item.ProgressPercentage,
                Status = GetStepStatus(item),
                ApprovalStatus = item.ApprovalStatus.ToString(),
                PlannedStartDate = item.PlannedStartDate,
                PlannedEndDate = item.PlannedEndDate,
                ActualStartDate = item.ActualStartDate,
                ActualEndDate = item.ActualEndDate,
                IsDelayed = IsStepDelayed(item),
                ProjectId = item.ProjectId,
                ProjectName = item.Project?.NameAr,
                Notes = item.Notes,
                CompletionDetails = item.CompletionDetails,
                RejectionReason = item.RejectionReason,
                ApproverNotes = item.ApproverNotes,
                ApprovedAt = item.ApprovedAt,
                SubmittedForApprovalAt = item.SubmittedForApprovalAt
            };

            return Ok(new ApiResponse<StepDetailDto> { Data = dto });
        }

        // ================================================================
        //  Overdue / Alerts
        // ================================================================

        /// <summary>
        /// GET /api/data/overdue — all overdue items across the system
        /// </summary>
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdue(int? fiscalYearId)
        {
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Where(i => !fiscalYearId.HasValue || i.FiscalYearId == fiscalYearId)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .ToListAsync();

            var result = new List<OverdueItemDto>();

            foreach (var i in initiatives)
            {
                foreach (var p in i.Projects.Where(p => IsDelayed(p)))
                {
                    var dueDate = p.ActualEndDate ?? p.PlannedEndDate;
                    result.Add(new OverdueItemDto
                    {
                        Id = p.Id, Code = p.Code, Name = p.NameAr, Type = "Project",
                        Progress = p.ProgressPercentage,
                        DueDate = dueDate,
                        DaysOverdue = dueDate.HasValue ? (int)(DateTime.Today - dueDate.Value).TotalDays : 0
                    });

                    foreach (var s in p.Steps.Where(s => IsStepDelayed(s)))
                    {
                        var stepDue = s.ActualEndDate ?? s.PlannedEndDate;
                        result.Add(new OverdueItemDto
                        {
                            Id = s.Id, Code = $"S{s.StepNumber}", Name = s.NameAr, Type = "Step",
                            Progress = s.ProgressPercentage,
                            DueDate = stepDue,
                            DaysOverdue = stepDue < DateTime.Today ? (int)(DateTime.Today - stepDue).TotalDays : 0
                        });
                    }
                }
            }

            return Ok(new ApiResponse<List<OverdueItemDto>>
            {
                Data = result.OrderByDescending(x => x.DaysOverdue).ToList(),
                TotalCount = result.Count
            });
        }

        /// <summary>
        /// GET /api/data/unit-performance — performance by organizational unit
        /// </summary>
        [HttpGet("unit-performance")]
        public async Task<IActionResult> GetUnitPerformance(int? fiscalYearId)
        {
            var initiatives = await _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Where(i => !fiscalYearId.HasValue || i.FiscalYearId == fiscalYearId)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                .ToListAsync();

            var groups = initiatives
                .Where(i => i.ExternalUnitId.HasValue)
                .GroupBy(i => new { i.ExternalUnitId, UnitName = i.ExternalUnitName ?? "غير محدد" })
                .Select(g => new UnitPerformanceDto
                {
                    UnitId = g.Key.ExternalUnitId,
                    UnitName = g.Key.UnitName,
                    InitiativeCount = g.Count(),
                    ProjectCount = g.SelectMany(i => i.Projects).Count(),
                    AverageProgress = g.SelectMany(i => i.Projects).Any()
                        ? Math.Round(g.SelectMany(i => i.Projects).Average(p => p.ProgressPercentage), 1) : 0,
                    CompletedCount = g.Count(i => i.Projects.Any() && i.Projects.All(p => p.ProgressPercentage >= 100)),
                    DelayedCount = g.Count(i => i.Projects.Any(p => IsDelayed(p)))
                })
                .OrderByDescending(u => u.InitiativeCount)
                .ToList();

            return Ok(new ApiResponse<List<UnitPerformanceDto>>
            {
                Data = groups,
                TotalCount = groups.Count
            });
        }


        // ================================================================
        //  Chat Context — Role-Filtered Data for AI Chatbot
        // ================================================================

        /// <summary>
        /// GET /api/data/chat-context?userId=1&role=Admin
        /// يرجع بيانات مصفّاة حسب صلاحيات المستخدم لاستخدامها كسياق للمحادثة مع الذكاء الاصطناعي.
        /// 
        /// قواعد التصفية:
        ///   Admin / Executive → جميع البيانات
        ///   Supervisor        → فقط المبادرات التي يشرف عليها (SupervisorId == userId) ومشاريعها وخطواتها
        ///   غير ذلك           → 403 Forbidden
        /// </summary>
        [HttpGet("chat-context")]
        public async Task<IActionResult> GetChatContext(int userId, string role)
        {
            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid role" });

            // فقط Admin / Executive / Supervisor يمكنهم استخدام المساعد الذكي
            if (userRole != UserRole.Admin && userRole != UserRole.Executive && userRole != UserRole.Supervisor)
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Access denied — insufficient role" });

            // ===== جلب المبادرات مع التصفية حسب الدور =====
            var query = _db.Initiatives
                .Where(i => !i.IsDeleted)
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Steps.Where(s => !s.IsDeleted))
                .Include(i => i.Projects.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.ProjectKPIs)
                .AsQueryable();

            // المشرف يرى فقط مبادراته
            if (userRole == UserRole.Supervisor)
                query = query.Where(i => i.SupervisorId == userId);

            var initiatives = await query.ToListAsync();
            var projects = initiatives.SelectMany(i => i.Projects).ToList();
            var steps = projects.SelectMany(p => p.Steps).ToList();

            // ===== بناء السياق =====
            var context = new ChatContextDto
            {
                UserName = (await _db.Users.FindAsync(userId))?.FullNameAr ?? "مستخدم",
                UserRole = role,
                GeneratedAt = DateTime.Now,

                Summary = new ChatSummaryDto
                {
                    TotalInitiatives = initiatives.Count,
                    TotalProjects = projects.Count,
                    TotalSteps = steps.Count,
                    CompletedProjects = projects.Count(p => p.ProgressPercentage >= 100),
                    DelayedProjects = projects.Count(p => IsDelayed(p)),
                    DelayedSteps = steps.Count(s => IsStepDelayed(s)),
                    AverageProgress = projects.Any() ? Math.Round(projects.Average(p => p.ProgressPercentage), 1) : 0
                },

                Initiatives = initiatives.Select(i => new ChatInitiativeDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    Name = i.NameAr,
                    Unit = i.ExternalUnitName,
                    Supervisor = i.SupervisorName,
                    Status = GetInitiativeStatus(i),
                    Progress = i.Projects.Any()
                        ? Math.Round(i.Projects.Where(p => !p.IsDeleted).Average(p => p.ProgressPercentage), 1) : 0,
                    Budget = i.Budget,
                    PlannedEnd = i.PlannedEndDate,

                    Projects = i.Projects.Select(p => new ChatProjectDto
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.NameAr,
                        Manager = p.ProjectManagerName,
                        Status = GetProjectStatus(p),
                        Progress = p.ProgressPercentage,
                        IsDelayed = IsDelayed(p),
                        Budget = p.Budget,
                        PlannedEnd = p.PlannedEndDate,

                        Steps = p.Steps.OrderBy(s => s.StepNumber).Select(s => new ChatStepDto
                        {
                            Id = s.Id,
                            Number = s.StepNumber,
                            Name = s.NameAr,
                            AssignedTo = s.AssignedToName,
                            Status = GetStepStatus(s),
                            Progress = s.ProgressPercentage,
                            Weight = s.Weight,
                            IsDelayed = IsStepDelayed(s),
                            PlannedEnd = s.PlannedEndDate
                        }).ToList(),

                        KPIs = p.ProjectKPIs?.Select(k => new ChatKpiDto
                        {
                            Name = k.KPIText,
                            Target = k.TargetValue,
                            Actual = k.ActualValue
                        }).ToList() ?? new()

                    }).ToList()

                }).ToList()
            };

            return Ok(new ApiResponse<ChatContextDto> { Data = context });
        }

        // ================================================================
        //  Mapping Helpers (Entity → DTO)
        // ================================================================

        private static FiscalYearDto MapFiscalYear(FiscalYear f) => new()
        {
            Id = f.Id, Year = f.Year, NameAr = f.NameAr, NameEn = f.NameEn,
            StartDate = f.StartDate, EndDate = f.EndDate, IsCurrent = f.IsCurrent
        };

        private static InitiativeListDto MapInitiativeList(Initiative i) => new()
        {
            Id = i.Id, Code = i.Code, NameAr = i.NameAr, NameEn = i.NameEn,
            UnitName = i.ExternalUnitName,
            SupervisorName = i.SupervisorName ?? i.Supervisor?.FullNameAr,
            Budget = i.Budget, ActualCost = i.ActualCost,
            PlannedStartDate = i.PlannedStartDate, PlannedEndDate = i.PlannedEndDate,
            ActualStartDate = i.ActualStartDate, ActualEndDate = i.ActualEndDate,
            ProgressPercentage = i.Projects.Any()
                ? Math.Round(i.Projects.Where(p => !p.IsDeleted).Average(p => p.ProgressPercentage), 1) : 0,
            ProjectCount = i.Projects.Count(p => !p.IsDeleted),
            CompletedProjectCount = i.Projects.Count(p => !p.IsDeleted && p.ProgressPercentage >= 100),
            Status = GetInitiativeStatus(i),
            IsDelayed = i.Projects.Any(p => !p.IsDeleted && IsDelayed(p))
        };

        private static ProjectListDto MapProjectList(Project p) => new()
        {
            Id = p.Id, Code = p.Code, NameAr = p.NameAr, NameEn = p.NameEn,
            ProjectManagerName = p.ProjectManagerName,
            UnitName = p.ExternalUnitName,
            Budget = p.Budget, ActualCost = p.ActualCost,
            PlannedStartDate = p.PlannedStartDate, PlannedEndDate = p.PlannedEndDate,
            ActualStartDate = p.ActualStartDate, ActualEndDate = p.ActualEndDate,
            ProgressPercentage = p.ProgressPercentage,
            StepCount = p.Steps?.Count(s => !s.IsDeleted) ?? 0,
            CompletedStepCount = p.Steps?.Count(s => !s.IsDeleted && s.ProgressPercentage >= 100) ?? 0,
            Status = GetProjectStatus(p),
            IsDelayed = IsDelayed(p),
            InitiativeId = p.InitiativeId,
            InitiativeName = p.Initiative?.NameAr
        };

        private static StepListDto MapStepList(Step s) => new()
        {
            Id = s.Id, StepNumber = s.StepNumber, NameAr = s.NameAr, NameEn = s.NameEn,
            AssignedToName = s.AssignedToName ?? s.AssignedTo?.FullNameAr,
            Weight = s.Weight, ProgressPercentage = s.ProgressPercentage,
            Status = GetStepStatus(s),
            ApprovalStatus = s.ApprovalStatus.ToString(),
            PlannedStartDate = s.PlannedStartDate, PlannedEndDate = s.PlannedEndDate,
            ActualStartDate = s.ActualStartDate, ActualEndDate = s.ActualEndDate,
            IsDelayed = IsStepDelayed(s),
            ProjectId = s.ProjectId,
            ProjectName = s.Project?.NameAr
        };

        // ================================================================
        //  Status Helpers (return Arabic string, not enum int)
        // ================================================================

        private static string GetStepStatus(Step s)
        {
            if (s.ProgressPercentage >= 100) return "مكتمل";
            if (s.Status == StepStatus.Cancelled) return "ملغي";
            if (s.Status == StepStatus.OnHold) return "متوقف";
            if (IsStepDelayed(s)) return "متأخر";
            if (s.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        private static string GetProjectStatus(Project p)
        {
            if (p.ProgressPercentage >= 100) return "مكتمل";
            if (IsDelayed(p)) return "متأخر";
            if (p.ProgressPercentage > 0) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        private static string GetInitiativeStatus(Initiative i)
        {
            if (!i.Projects.Any(p => !p.IsDeleted)) return "لم يبدأ";
            var active = i.Projects.Where(p => !p.IsDeleted).ToList();
            if (active.All(p => p.ProgressPercentage >= 100)) return "مكتمل";
            if (active.Any(p => IsDelayed(p))) return "متأخر";
            if (active.Any(p => p.ProgressPercentage > 0)) return "قيد التنفيذ";
            return "لم يبدأ";
        }

        private static bool IsStepDelayed(Step s)
        {
            if (s.ProgressPercentage >= 100 || s.Status == StepStatus.Cancelled) return false;
            if (s.ActualEndDate.HasValue && s.ActualEndDate.Value < DateTime.Today) return true;
            return s.Status == StepStatus.Delayed;
        }

        private static bool IsDelayed(Project p)
        {
            if (p.ProgressPercentage >= 100) return false;
            if (p.Steps?.Any(s => !s.IsDeleted && IsStepDelayed(s)) == true) return true;
            if (p.ActualEndDate.HasValue && p.ActualEndDate.Value < DateTime.Today) return true;
            return false;
        }
    }
}
