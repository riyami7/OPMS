using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Services
{
    public interface IStepService
    {
        // القراءة
        Task<StepListViewModel> GetListAsync(StepListViewModel filters, UserRole userRole, int userId);
        Task<List<Step>> GetPendingApprovalsAsync();
        Task<StepDetailsViewModel?> GetDetailsAsync(int id);
        Task<Step?> GetByIdAsync(int id);
        Task<Step?> GetWithProjectAsync(int id);

        // CRUD
        Task<(StepFormViewModel? ViewModel, decimal UsedWeight, decimal RemainingWeight, string? ProjectName, string? InitiativeName)> PrepareCreateViewModelAsync(int projectId);
        Task<(StepFormViewModel? ViewModel, decimal UsedWeight, decimal RemainingWeight, string? ProjectName, string? InitiativeName)> PrepareEditViewModelAsync(int id);
        Task<(bool Success, int? ProjectId, string? Error, string? Warning)> CreateAsync(StepFormViewModel model, int createdById);
        Task<(bool Success, string? Error, string? Warning)> UpdateAsync(int id, StepFormViewModel model, int modifiedById);
        Task<(bool Success, int? ProjectId, string? Error)> SoftDeleteAsync(int id, int modifiedById);

        // سير العمل
        Task<(bool Success, string? Error)> UpdateProgressAsync(int id, decimal progress, string? notes, int userId);
        Task<(bool Success, string? Error)> SubmitForApprovalAsync(int id, string completionDetails, Microsoft.AspNetCore.Http.IFormFile? attachmentFile, int userId, string webRootPath);
        Task<(bool Success, string? Error)> ApproveStepAsync(int id, string? approverNotes, int approverId);
        Task<(bool Success, string? Error)> RejectStepAsync(int id, string rejectionReason, int rejecterId);

        // الملاحظات
        Task<(bool Success, string? Error)> AddNoteAsync(int stepId, string notes, int createdById);
        Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int stepId, string notes);
        Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int stepId);

        // المساعدة
        Task PopulateFormDropdownsAsync(StepFormViewModel model);
        bool CanAccessStep(Step step, UserRole userRole, int userId);
        bool CanEditProject(Project project, UserRole userRole, int userId);
        Task<bool> IsStepApproverAsync(int userId);
    }

    public class StepService : IStepService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<StepService> _logger;
        private readonly IAuditService _audit;
        private readonly INotificationService _notify;
        private readonly IUserService _userService;

        public StepService(AppDbContext db, ILogger<StepService> logger, IAuditService audit, INotificationService notify, IUserService userService)
        {
            _db = db;
            _logger = logger;
            _audit = audit;
            _notify = notify;
            _userService = userService;
        }

        #region القراءة

        public async Task<StepListViewModel> GetListAsync(StepListViewModel filters, UserRole userRole, int userId)
        {
            var query = _db.Steps.Where(s => !s.IsDeleted)
                .Include(s => s.Project).ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo).AsQueryable();

            if (userRole == UserRole.Supervisor)
            {
                var supervisedIds = await _db.Initiatives.Where(i => i.SupervisorId == userId && !i.IsDeleted).Select(i => i.Id).ToListAsync();
                var projectIds = await _db.Projects.Where(p => supervisedIds.Contains(p.InitiativeId) && !p.IsDeleted).Select(p => p.Id).ToListAsync();
                query = query.Where(s => projectIds.Contains(s.ProjectId));
            }
            else if (userRole == UserRole.User)
            {
                var projectIds = await _db.Projects.Where(p => p.ProjectManagerId == userId && !p.IsDeleted).Select(p => p.Id).ToListAsync();
                query = query.Where(s => projectIds.Contains(s.ProjectId));
            }
            else if (userRole == UserRole.StepUser)
                query = query.Where(s => s.AssignedToId == userId);

            if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
                query = query.Where(s => s.NameAr.Contains(filters.SearchTerm) || s.NameEn.Contains(filters.SearchTerm));
            if (filters.StatusFilter.HasValue)
                query = query.Where(s => s.Status == filters.StatusFilter.Value);
            if (filters.ProjectId.HasValue)
                query = query.Where(s => s.ProjectId == filters.ProjectId.Value);

            filters.TotalCount = await query.CountAsync();
            filters.Steps = await query.OrderBy(s => s.ProjectId).ThenBy(s => s.StepNumber)
                .Skip((filters.CurrentPage - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

            foreach (var step in filters.Steps) UpdateStepDelayedStatus(step);
            await PopulateFilterDropdownsAsync(filters, userRole, userId);
            return filters;
        }

        public async Task<List<Step>> GetPendingApprovalsAsync()
        {
            return await _db.Steps.Where(s => !s.IsDeleted && s.ApprovalStatus == ApprovalStatus.Pending)
                .Include(s => s.Project).ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo).Include(s => s.Attachments)
                .OrderBy(s => s.SubmittedForApprovalAt).ToListAsync();
        }

        public async Task<StepDetailsViewModel?> GetDetailsAsync(int id)
        {
            var step = await _db.Steps
                .Include(s => s.Project).ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo).Include(s => s.CreatedBy)
                .Include(s => s.DependsOnStep).Include(s => s.ApprovedBy)
                .Include(s => s.Attachments).ThenInclude(a => a.UploadedBy)
                .Include(s => s.TeamMembers)
                .Include(s => s.KPIs)
                .Include(s => s.StepSupportingUnits).ThenInclude(su => su.ProjectSupportingUnit)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return null;

            UpdateStepDelayedStatus(step);
            return new StepDetailsViewModel
            {
                Step = step,
                Notes = await _db.ProgressUpdates.Where(p => p.StepId == id)
                    .Include(p => p.CreatedBy).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync()
            };
        }

        public async Task<Step?> GetByIdAsync(int id) =>
            await _db.Steps.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        public async Task<Step?> GetWithProjectAsync(int id) =>
            await _db.Steps.Include(s => s.Project).ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo).FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        #endregion

        #region CRUD

        public async Task<(StepFormViewModel? ViewModel, decimal UsedWeight, decimal RemainingWeight, string? ProjectName, string? InitiativeName)> PrepareCreateViewModelAsync(int projectId)
        {
            var project = await _db.Projects.Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .Include(p => p.SupportingUnits)
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project == null) return (null, 0, 0, null, null);

            var usedWeight = project.Steps.Sum(s => s.Weight);
            var remainingWeight = 100 - usedWeight;

            var viewModel = new StepFormViewModel
            {
                ProjectId = projectId,
                ProjectName = project.NameAr,
                Weight = remainingWeight > 0 ? Math.Min(remainingWeight, 10) : 10,
                StepNumber = project.Steps.Any() ? project.Steps.Max(s => s.StepNumber) + 1 : 1,
                AvailableSupportingUnits = project.SupportingUnits.Select(su => new SupportingUnitOption
                {
                    ProjectSupportingUnitId = su.Id,
                    UnitName = su.ExternalUnitName ?? "",
                    IsSelected = false
                }).ToList()
            };
            await PopulateFormDropdownsAsync(viewModel);
            return (viewModel, usedWeight, remainingWeight, project.NameAr, project.Initiative?.NameAr);
        }

        public async Task<(StepFormViewModel? ViewModel, decimal UsedWeight, decimal RemainingWeight, string? ProjectName, string? InitiativeName)> PrepareEditViewModelAsync(int id)
        {
            var step = await _db.Steps.Include(s => s.Project).ThenInclude(p => p.Initiative)
                .Include(s => s.Project).ThenInclude(p => p.Steps.Where(st => !st.IsDeleted))
                .Include(s => s.Project).ThenInclude(p => p.SupportingUnits)
                .Include(s => s.TeamMembers)
                .Include(s => s.KPIs)
                .Include(s => s.StepSupportingUnits)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (null, 0, 0, null, null);

            var viewModel = StepFormViewModel.FromEntity(step);
            // تعبئة الجهات المساندة المتاحة مع تحديد المختارة
            viewModel.AvailableSupportingUnits = step.Project.SupportingUnits.Select(su => new SupportingUnitOption
            {
                ProjectSupportingUnitId = su.Id,
                UnitName = su.ExternalUnitName ?? "",
                IsSelected = viewModel.SelectedSupportingUnitIds.Contains(su.Id)
            }).ToList();
            await PopulateFormDropdownsAsync(viewModel);
            var usedWeight = step.Project.Steps.Where(s => s.Id != id).Sum(s => s.Weight);
            return (viewModel, usedWeight, 100 - usedWeight, step.Project?.NameAr, step.Project?.Initiative?.NameAr);
        }

        public async Task<(bool Success, int? ProjectId, string? Error, string? Warning)> CreateAsync(StepFormViewModel model, int createdById)
        {
            var project = await _db.Projects.Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.IsDeleted);
            if (project == null) return (false, null, "المشروع غير موجود", null);

            var remainingWeight = 100 - project.Steps.Sum(s => s.Weight);
            if (model.Weight > remainingWeight)
                return (false, model.ProjectId, $"الوزن المتبقي للمشروع هو {remainingWeight}% فقط", null);

            var step = new Step
            {
                CreatedById = createdById, CreatedAt = DateTime.Now,
                InitiativeId = project.InitiativeId,
                Status = StepStatus.NotStarted, ApprovalStatus = ApprovalStatus.None
            };
            model.UpdateEntity(step);
            step.AssignedToId = await ResolveUserIdAsync(model.AssignedToEmpNumber, model.AssignedToName, model.AssignedToRank);

            string? warning = null;
            if (!string.IsNullOrWhiteSpace(model.AssignedToEmpNumber) && step.AssignedToId == null)
                warning = $"تنبيه: مسؤول الخطوة ({model.AssignedToName}) غير مسجّل في النظام.";

            _db.Steps.Add(step);
            await _db.SaveChangesAsync();

            // حفظ فريق العمل
            if (model.TeamMembers?.Any() == true)
            {
                var members = model.TeamMembers
                    .Where(m => !string.IsNullOrWhiteSpace(m.EmpNumber))
                    .Select((m, i) => new StepTeamMember
                    {
                        StepId = step.Id,
                        EmpNumber = m.EmpNumber,
                        Name = m.Name,
                        Rank = m.Rank,
                        Role = m.Role,
                        OrderIndex = i,
                        CreatedAt = DateTime.Now
                    }).ToList();
                if (members.Any())
                {
                    _db.StepTeamMembers.AddRange(members);
                    await _db.SaveChangesAsync();
                }
            }

            // حفظ مؤشرات الأداء
            if (model.KPIIndicators?.Any() == true)
            {
                var kpis = model.KPIIndicators
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select((k, i) => new StepKPI
                    {
                        StepId = step.Id,
                        Indicator = k.Trim(),
                        OrderIndex = i,
                        CreatedAt = DateTime.Now
                    }).ToList();
                if (kpis.Any())
                {
                    _db.StepKPIs.AddRange(kpis);
                    await _db.SaveChangesAsync();
                }
            }

            // حفظ الجهات المساندة
            if (model.SelectedSupportingUnitIds?.Any() == true)
            {
                var stepUnits = model.SelectedSupportingUnitIds.Select(id => new StepSupportingUnit
                {
                    StepId = step.Id,
                    ProjectSupportingUnitId = id
                }).ToList();
                _db.StepSupportingUnits.AddRange(stepUnits);
                await _db.SaveChangesAsync();
            }

            await UpdateProjectProgressAsync(project.Id);

            _logger.LogInformation("تم إنشاء خطوة: {StepNumber} في مشروع {ProjectId}", step.StepNumber, step.ProjectId);
            await _audit.LogAsync("Step", step.Id, step.NameAr, "Create", createdById, $"خطوة #{step.StepNumber} في مشروع #{step.ProjectId}");

            // إشعار للمنفذ المعيّن
            if (step.AssignedToId.HasValue && step.AssignedToId.Value != createdById)
            {
                await _notify.CreateAsync(step.AssignedToId.Value,
                    "تم تعيينك على خطوة جديدة",
                    $"{step.NameAr} - في مشروع {project.NameAr}",
                    "StepAssigned", $"/Steps/Details/{step.Id}", "bi-person-plus");
            }

            return (true, step.ProjectId, null, warning);
        }

        public async Task<(bool Success, string? Error, string? Warning)> UpdateAsync(int id, StepFormViewModel model, int modifiedById)
        {
            var step = await _db.Steps.Include(s => s.Project).ThenInclude(p => p.Steps.Where(st => !st.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة", null);

            var oldAssignedToId = step.AssignedToId; // حفظ المنفذ القديم

            var remainingWeight = 100 - step.Project.Steps.Where(s => s.Id != id).Sum(s => s.Weight);
            if (model.Weight > remainingWeight)
                return (false, $"الوزن المتبقي للمشروع هو {remainingWeight}% فقط", null);

            model.UpdateEntity(step);
            step.AssignedToId = await ResolveUserIdAsync(model.AssignedToEmpNumber, model.AssignedToName, model.AssignedToRank);

            string? warning = null;
            if (!string.IsNullOrWhiteSpace(model.AssignedToEmpNumber) && step.AssignedToId == null)
                warning = $"تنبيه: مسؤول الخطوة ({model.AssignedToName}) غير مسجّل في النظام.";

            // حذف فريق العمل القديم وإعادة حفظه
            _db.StepTeamMembers.RemoveRange(await _db.StepTeamMembers.Where(t => t.StepId == id).ToListAsync());
            if (model.TeamMembers?.Any() == true)
            {
                var members = model.TeamMembers
                    .Where(m => !string.IsNullOrWhiteSpace(m.EmpNumber))
                    .Select((m, i) => new StepTeamMember
                    {
                        StepId = id, EmpNumber = m.EmpNumber, Name = m.Name,
                        Rank = m.Rank, Role = m.Role, OrderIndex = i, CreatedAt = DateTime.Now
                    }).ToList();
                if (members.Any()) _db.StepTeamMembers.AddRange(members);
            }

            // حذف وإعادة إضافة مؤشرات الأداء
            _db.StepKPIs.RemoveRange(await _db.StepKPIs.Where(k => k.StepId == id).ToListAsync());
            if (model.KPIIndicators?.Any() == true)
            {
                var kpis = model.KPIIndicators
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select((k, i) => new StepKPI
                    {
                        StepId = id, Indicator = k.Trim(), OrderIndex = i, CreatedAt = DateTime.Now
                    }).ToList();
                if (kpis.Any()) _db.StepKPIs.AddRange(kpis);
            }

            // حذف وإعادة إضافة الجهات المساندة
            _db.StepSupportingUnits.RemoveRange(await _db.StepSupportingUnits.Where(s => s.StepId == id).ToListAsync());
            if (model.SelectedSupportingUnitIds?.Any() == true)
            {
                _db.StepSupportingUnits.AddRange(model.SelectedSupportingUnitIds.Select(uid => new StepSupportingUnit
                {
                    StepId = id, ProjectSupportingUnitId = uid
                }));
            }

            // إذا النسبة أقل من 100 وكانت مؤكدة/مرفوضة — أعد الحالة لـ None
            if (step.ProgressPercentage < 100 && step.ApprovalStatus != ApprovalStatus.None)
            {
                step.ApprovalStatus = ApprovalStatus.None;
                step.RejectionReason = null;
            }

            step.LastModifiedById = modifiedById;
            step.LastModifiedAt = DateTime.Now;
            UpdateStepDelayedStatus(step);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Step", step.Id, step.NameAr, "Update", modifiedById, $"خطوة #{step.StepNumber}");

            // إشعار للمنفذ الجديد إذا تغيّر
            if (step.AssignedToId.HasValue && step.AssignedToId != oldAssignedToId && step.AssignedToId.Value != modifiedById)
            {
                await _notify.CreateAsync(step.AssignedToId.Value,
                    "تم تعيينك على خطوة",
                    $"{step.NameAr} - في مشروع {step.Project.NameAr}",
                    "StepAssigned", $"/Steps/Details/{step.Id}", "bi-person-plus");
            }

            return (true, null, warning);
        }

        public async Task<(bool Success, int? ProjectId, string? Error)> SoftDeleteAsync(int id, int modifiedById)
        {
            var step = await _db.Steps.Include(s => s.Project).FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, null, "الخطوة غير موجودة");

            if (await _db.Steps.AnyAsync(s => s.DependsOnStepId == id && !s.IsDeleted))
                return (false, step.ProjectId, "لا يمكن حذف هذه الخطوة لأن هناك خطوات أخرى تعتمد عليها");

            var projectId = step.ProjectId;
            step.IsDeleted = true;
            step.LastModifiedById = modifiedById;
            step.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Step", step.Id, step.NameAr, "Delete", modifiedById, $"خطوة #{step.StepNumber}");
            await UpdateProjectProgressAsync(projectId);
            return (true, projectId, null);
        }

        #endregion

        #region سير العمل

        public async Task<(bool Success, string? Error)> UpdateProgressAsync(int id, decimal progress, string? notes, int userId)
        {
            var step = await _db.Steps.Include(s => s.Project).FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة");
            if (step.ApprovalStatus == ApprovalStatus.Pending) return (false, "لا يمكن تعديل خطوة معلقة للتأكيد");
            if (progress >= 100) return (false, "لإكمال الخطوة استخدم زر 'إرسال للتأكيد' مع إرفاق ملف التوثيق");

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                StepId = id, PreviousPercentage = step.ProgressPercentage, ProgressPercentage = progress,
                NotesAr = notes, UpdateType = UpdateType.StatusChange, CreatedById = userId, CreatedAt = DateTime.Now
            });

            step.ProgressPercentage = progress;
            step.LastModifiedById = userId;
            step.LastModifiedAt = DateTime.Now;
            if (progress > 0) { step.Status = StepStatus.InProgress; step.ActualStartDate ??= DateTime.Today; }
            UpdateStepDelayedStatus(step);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Step", id, step.NameAr, "UpdateProgress", userId, $"{step.ProgressPercentage}%");
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SubmitForApprovalAsync(
            int id, string completionDetails, Microsoft.AspNetCore.Http.IFormFile? attachmentFile, int userId, string webRootPath)
        {
            var step = await _db.Steps.Include(s => s.Project).FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة");
            if (step.ApprovalStatus == ApprovalStatus.Pending) return (false, "الخطوة معلقة للتأكيد بالفعل");

            // معالجة المرفق إذا وجد
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var allowedExt = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                var ext = Path.GetExtension(attachmentFile.FileName).ToLowerInvariant();
                if (!allowedExt.Contains(ext)) return (false, "نوع الملف غير مسموح. الأنواع المسموحة: PDF, JPG, PNG");
                if (attachmentFile.Length > 10 * 1024 * 1024) return (false, "حجم الملف يجب أن يكون أقل من 10 ميجابايت");

                var uploadsFolder = Path.Combine(webRootPath, "uploads", "steps", id.ToString());
                Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"S{id}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await attachmentFile.CopyToAsync(stream);

                _db.StepAttachments.Add(new StepAttachment
                {
                    StepId = id, FileName = uniqueFileName, OriginalFileName = attachmentFile.FileName,
                    ContentType = attachmentFile.ContentType, FileSize = attachmentFile.Length,
                    FilePath = $"/uploads/steps/{id}/{uniqueFileName}",
                    Description = "مرفق توثيق إتمام الخطوة", UploadedById = userId, UploadedAt = DateTime.Now
                });
            }

            step.ProgressPercentage = 100;
            step.Status = StepStatus.InProgress;
            step.ApprovalStatus = ApprovalStatus.Pending;
            step.CompletionDetails = completionDetails;
            step.SubmittedForApprovalAt = DateTime.Now;
            step.RejectionReason = null;
            step.LastModifiedById = userId;
            step.LastModifiedAt = DateTime.Now;
            step.ActualStartDate ??= DateTime.Today;

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                StepId = id, PreviousPercentage = step.ProgressPercentage, ProgressPercentage = 100,
                NotesAr = "تم إرسال الخطوة للتأكيد", UpdateType = UpdateType.StatusChange,
                CreatedById = userId, CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ApproveStepAsync(int id, string? approverNotes, int approverId)
        {
            var step = await _db.Steps.Include(s => s.Project).FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة");
            if (step.ApprovalStatus != ApprovalStatus.Pending) return (false, "الخطوة ليست معلقة للتأكيد");

            step.ApprovalStatus = ApprovalStatus.Approved;
            step.Status = StepStatus.Completed;
            step.ApprovedById = approverId;
            step.ApprovedAt = DateTime.Now;
            step.ActualEndDate = DateTime.Today;
            step.ApproverNotes = approverNotes;
            step.LastModifiedById = approverId;
            step.LastModifiedAt = DateTime.Now;

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                StepId = id, ProgressPercentage = 100,
                NotesAr = string.IsNullOrWhiteSpace(approverNotes)
                    ? "تم تأكيد إكمال الخطوة"
                    : $"تم تأكيد إكمال الخطوة - ملاحظة المؤكد: {approverNotes}",
                UpdateType = UpdateType.StatusChange, CreatedById = approverId, CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Step", id, step.NameAr, "Approve", approverId, approverNotes);

            // إشعار للمنفذ
            if (step.AssignedToId.HasValue)
            {
                await _notify.CreateAsync(step.AssignedToId.Value,
                    "تم اعتماد خطوتك",
                    $"{step.NameAr} - تم التأكيد بنجاح",
                    "StepApproved", $"/Steps/Details/{id}", "bi-check-circle");
            }

            await UpdateProjectProgressAsync(step.ProjectId);
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> RejectStepAsync(int id, string rejectionReason, int rejecterId)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason)) return (false, "يجب كتابة سبب الرفض");
            var step = await _db.Steps.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة");
            if (step.ApprovalStatus != ApprovalStatus.Pending) return (false, "الخطوة ليست معلقة للتأكيد");

            step.ApprovalStatus = ApprovalStatus.Rejected;
            step.Status = StepStatus.InProgress;
            step.ProgressPercentage = 99;
            step.RejectionReason = rejectionReason;
            step.LastModifiedById = rejecterId;
            step.LastModifiedAt = DateTime.Now;

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                StepId = id, ProgressPercentage = 99,
                NotesAr = $"تم رفض الخطوة - السبب: {rejectionReason}",
                UpdateType = UpdateType.StatusChange, CreatedById = rejecterId, CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Step", id, step.NameAr, "Reject", rejecterId, rejectionReason);

            // إشعار للمنفذ
            if (step.AssignedToId.HasValue)
            {
                await _notify.CreateAsync(step.AssignedToId.Value,
                    "تم رفض خطوتك",
                    $"{step.NameAr} - السبب: {rejectionReason}",
                    "StepRejected", $"/Steps/Details/{id}", "bi-x-circle");
            }

            return (true, null);
        }

        #endregion

        #region الملاحظات

        public async Task<(bool Success, string? Error)> AddNoteAsync(int stepId, string notes, int createdById)
        {
            if (string.IsNullOrWhiteSpace(notes)) return (false, "الملاحظة مطلوبة");
            var step = await _db.Steps.FirstOrDefaultAsync(s => s.Id == stepId && !s.IsDeleted);
            if (step == null) return (false, "الخطوة غير موجودة");

            _db.ProgressUpdates.Add(new ProgressUpdate
            {
                StepId = stepId, NotesAr = notes, UpdateType = UpdateType.Note,
                CreatedById = createdById, CreatedAt = DateTime.Now
            });
            step.LastModifiedById = createdById;
            step.LastModifiedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> EditNoteAsync(int noteId, int stepId, string notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return (false, "الملاحظة مطلوبة");
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.StepId != stepId) return (false, "الملاحظة غير موجودة");
            note.NotesAr = notes;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteNoteAsync(int noteId, int stepId)
        {
            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.StepId != stepId) return (false, "الملاحظة غير موجودة");
            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        #endregion

        #region المساعدة

        public bool CanAccessStep(Step step, UserRole userRole, int userId)
        {
            if (userRole == UserRole.Admin || userRole == UserRole.Executive) return true;
            var project = step.Project ?? _db.Projects.Include(p => p.Initiative).FirstOrDefault(p => p.Id == step.ProjectId);
            if (project == null) return false;

            // منفذ الخطوة
            if (step.AssignedToId == userId) return true;

            // مدير المشروع
            if (project.ProjectManagerId == userId) return true;

            // مساعد مدير المشروع
            if (!string.IsNullOrEmpty(project.DeputyManagerEmpNumber))
            {
                var deputyUser = _db.Users.FirstOrDefault(u => u.ADUsername == project.DeputyManagerEmpNumber && u.IsActive);
                if (deputyUser?.Id == userId) return true;
            }

            // مشرف المبادرة
            if (project.Initiative?.SupervisorId == userId) return true;

            // Fallback: check InitiativeAccess
            return _db.InitiativeAccess.Any(a =>
                a.InitiativeId == project.InitiativeId &&
                a.UserId == userId &&
                a.IsActive);
        }

        public bool CanEditProject(Project project, UserRole userRole, int userId)
        {
            if (userRole == UserRole.Admin) return true;
            if (userRole == UserRole.Supervisor)
            {
                var initiative = project.Initiative ?? _db.Initiatives.FirstOrDefault(i => i.Id == project.InitiativeId);
                if (initiative?.SupervisorId == userId || project.ProjectManagerId == userId)
                    return true;
            }
            if (userRole == UserRole.User && project.ProjectManagerId == userId)
                return true;

            // Fallback: check InitiativeAccess for Contributor or FullAccess
            var access = _db.InitiativeAccess
                .FirstOrDefault(a => a.InitiativeId == project.InitiativeId && a.UserId == userId && a.IsActive);
            return access != null && access.AccessLevel >= AccessLevel.Contributor;
        }

        public async Task<bool> IsStepApproverAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            return user?.IsStepApprover ?? false;
        }

        public async Task PopulateFormDropdownsAsync(StepFormViewModel model)
        {
            model.Users = new SelectList(await _db.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullNameAr", model.AssignedToId);
            var otherSteps = await _db.Steps.Where(s => !s.IsDeleted && s.Id != model.Id && s.ProjectId == model.ProjectId)
                .OrderBy(s => s.StepNumber).Select(s => new { s.Id, Name = s.StepNumber + " - " + s.NameAr }).ToListAsync();
            model.DependsOnSteps = new SelectList(otherSteps, "Id", "Name", model.DependsOnStepId);
        }

        #endregion

        #region Private

        private void UpdateStepDelayedStatus(Step step)
        {
            if (step.Status == StepStatus.Completed || step.Status == StepStatus.Cancelled) return;
            if (step.ActualEndDate.HasValue && step.ActualEndDate.Value < DateTime.Today && step.ProgressPercentage < 100)
                step.Status = StepStatus.Delayed;
        }

        private async Task UpdateProjectProgressAsync(int projectId)
        {
            var project = await _db.Projects.Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project == null) return;
            project.ProgressPercentage = project.Steps
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved && s.ProgressPercentage >= 100)
                .Sum(s => s.Weight);
            await _db.SaveChangesAsync();
        }

        private async Task<int?> ResolveUserIdAsync(string? empNumber, string? name = null, string? rank = null)
        {
            return await _userService.EnsureUserExistsAsync(empNumber, name, rank, "Step User");
        }

        private async Task PopulateFilterDropdownsAsync(StepListViewModel model, UserRole userRole, int userId)
        {
            var projectsQuery = _db.Projects.Where(p => !p.IsDeleted);
            if (userRole == UserRole.Supervisor)
            {
                var ids = await _db.Initiatives.Where(i => i.SupervisorId == userId && !i.IsDeleted).Select(i => i.Id).ToListAsync();
                projectsQuery = projectsQuery.Where(p => ids.Contains(p.InitiativeId));
            }
            else if (userRole == UserRole.User)
                projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == userId);

            model.Projects = new SelectList(await projectsQuery.OrderBy(p => p.NameAr).ToListAsync(), "Id", "NameAr", model.ProjectId);
            model.Statuses = new SelectList(
                Enum.GetValues<StepStatus>().Select(s => new { Value = (int)s, Text = GetStatusAr(s) }), "Value", "Text", model.StatusFilter);
        }

        private static string GetStatusAr(StepStatus s) => s switch
        {
            StepStatus.NotStarted => "لم تبدأ", StepStatus.InProgress => "جارية",
            StepStatus.Completed => "مكتملة", StepStatus.Delayed => "متأخرة",
            StepStatus.Cancelled => "ملغاة", StepStatus.OnHold => "معلقة", _ => s.ToString()
        };

        #endregion
    }
}
