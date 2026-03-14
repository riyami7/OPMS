using System.Text.Json.Serialization;

namespace OperationalPlanMS.Models.Api
{
    // ============================================================
    //  API Response DTOs — safe for external consumption
    //  No circular references, no sensitive data, enums as strings
    // ============================================================

    /// <summary>
    /// Standard API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public T? Data { get; set; }
        public int? TotalCount { get; set; }
        public string? Message { get; set; }
    }

    // ==================== Summary / Dashboard ====================

    public class DashboardSummaryDto
    {
        public int TotalInitiatives { get; set; }
        public int TotalProjects { get; set; }
        public int TotalSteps { get; set; }
        public int CompletedInitiatives { get; set; }
        public int CompletedProjects { get; set; }
        public int CompletedSteps { get; set; }
        public int DelayedProjects { get; set; }
        public int DelayedSteps { get; set; }
        public decimal AverageProgress { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalActualCost { get; set; }
        public FiscalYearDto? CurrentFiscalYear { get; set; }
    }

    // ==================== Fiscal Year ====================

    public class FiscalYearDto
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsCurrent { get; set; }
    }

    // ==================== Initiative ====================

    public class InitiativeListDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string? UnitName { get; set; }
        public string? SupervisorName { get; set; }
        public decimal ProgressPercentage { get; set; }
        public int ProjectCount { get; set; }
        public int CompletedProjectCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public decimal? ActualCost { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public bool IsDelayed { get; set; }
    }

    public class InitiativeDetailDto : InitiativeListDto
    {
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? StrategicObjective { get; set; }
        public int? FiscalYearId { get; set; }
        public string? FiscalYearName { get; set; }
        public List<ProjectListDto> Projects { get; set; } = new();
    }

    // ==================== Project ====================

    public class ProjectListDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string? ProjectManagerName { get; set; }
        public string? UnitName { get; set; }
        public decimal ProgressPercentage { get; set; }
        public int StepCount { get; set; }
        public int CompletedStepCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public decimal? ActualCost { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public bool IsDelayed { get; set; }
        public int InitiativeId { get; set; }
        public string? InitiativeName { get; set; }
    }

    public class ProjectDetailDto : ProjectListDto
    {
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? OperationalGoal { get; set; }
        public string? ExpectedOutcomes { get; set; }
        public string? RiskNotes { get; set; }
        public List<StepListDto> Steps { get; set; } = new();
        public List<string> Requirements { get; set; } = new();
        public List<KpiDto> KPIs { get; set; } = new();
    }

    // ==================== Step ====================

    public class StepListDto
    {
        public int Id { get; set; }
        public int StepNumber { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string? NameEn { get; set; }
        public string? AssignedToName { get; set; }
        public decimal Weight { get; set; }
        public decimal ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public bool IsDelayed { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }

    public class StepDetailDto : StepListDto
    {
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? Notes { get; set; }
        public string? CompletionDetails { get; set; }
        public string? RejectionReason { get; set; }
        public string? ApproverNotes { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? SubmittedForApprovalAt { get; set; }
    }

    // ==================== Supporting ====================

    public class KpiDto
    {
        public string KpiText { get; set; } = string.Empty;
        public string? TargetValue { get; set; }
        public string? ActualValue { get; set; }
    }

    public class UnitPerformanceDto
    {
        public int? UnitId { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public int InitiativeCount { get; set; }
        public int ProjectCount { get; set; }
        public decimal AverageProgress { get; set; }
        public int CompletedCount { get; set; }
        public int DelayedCount { get; set; }
    }

    public class OverdueItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Initiative", "Project", "Step"
        public decimal Progress { get; set; }
        public DateTime? DueDate { get; set; }
        public int DaysOverdue { get; set; }
    }

    // ================================================================
    //  Chat Context DTOs — أضفها في نهاية ملف ApiDtos.cs
    // ================================================================

    public class ChatContextDto
    {
        public string UserName { get; set; } = "";
        public string UserRole { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public ChatSummaryDto Summary { get; set; } = new();
        public List<ChatInitiativeDto> Initiatives { get; set; } = new();
    }

    public class ChatSummaryDto
    {
        public int TotalInitiatives { get; set; }
        public int TotalProjects { get; set; }
        public int TotalSteps { get; set; }
        public int CompletedProjects { get; set; }
        public int DelayedProjects { get; set; }
        public int DelayedSteps { get; set; }
        public decimal AverageProgress { get; set; }
    }

    public class ChatInitiativeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Unit { get; set; }
        public string? Supervisor { get; set; }
        public string Status { get; set; } = "";
        public decimal Progress { get; set; }
        public decimal? Budget { get; set; }
        public DateTime PlannedEnd { get; set; }
        public List<ChatProjectDto> Projects { get; set; } = new();
    }

    public class ChatProjectDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Manager { get; set; }
        public string Status { get; set; } = "";
        public decimal Progress { get; set; }
        public bool IsDelayed { get; set; }
        public decimal? Budget { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public List<ChatStepDto> Steps { get; set; } = new();
        public List<ChatKpiDto> KPIs { get; set; } = new();
    }

    public class ChatStepDto
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string? AssignedTo { get; set; }
        public string Status { get; set; } = "";
        public decimal Progress { get; set; }
        public decimal Weight { get; set; }
        public bool IsDelayed { get; set; }
        public DateTime PlannedEnd { get; set; }
    }

    public class ChatKpiDto
    {
        public string Name { get; set; } = "";
        public string? Target { get; set; }
        public string? Actual { get; set; }
    }
}
