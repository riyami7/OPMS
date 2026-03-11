using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;
using OperationalPlanMS.Models.ViewModels;

namespace OperationalPlanMS.Controllers
{
    [Authorize]
    public class StepsController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public StepsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: /Steps
        public async Task<IActionResult> Index(StepListViewModel model)
        {
            var query = _db.Steps
                .Where(s => !s.IsDeleted)
                .Include(s => s.Project)
                    .ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo)
                .AsQueryable();

            // Apply role-based filtering
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Supervisor)
            {
                var supervisedInitiativeIds = await _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id)
                    .ToListAsync();
                var projectIds = await _db.Projects
                    .Where(p => supervisedInitiativeIds.Contains(p.InitiativeId) && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync();
                query = query.Where(s => projectIds.Contains(s.ProjectId));
            }
            else if (userRole == UserRole.User)
            {
                var userProjectIds = await _db.Projects
                    .Where(p => p.ProjectManagerId == userId && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync();
                query = query.Where(s => userProjectIds.Contains(s.ProjectId));
            }
            else if (userRole == UserRole.StepUser)
            {
                // StepUser يرى فقط الخطوات المعيّنة له
                query = query.Where(s => s.AssignedToId == userId);
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                query = query.Where(s =>
                    s.NameAr.Contains(model.SearchTerm) ||
                    s.NameEn.Contains(model.SearchTerm));
            }

            if (model.StatusFilter.HasValue)
            {
                query = query.Where(s => s.Status == model.StatusFilter.Value);
            }

            if (model.ProjectId.HasValue)
            {
                query = query.Where(s => s.ProjectId == model.ProjectId.Value);
            }

            model.TotalCount = await query.CountAsync();

            model.Steps = await query
                .OrderBy(s => s.ProjectId)
                .ThenBy(s => s.StepNumber)
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            foreach (var step in model.Steps)
            {
                UpdateStepDelayedStatus(step);
            }

            await PopulateFilterDropdowns(model);

            ViewBag.CanEdit = userRole == UserRole.Admin || userRole == UserRole.User;
            ViewBag.UserRole = userRole;

            return View(model);
        }

        // GET: /Steps/PendingApprovals - صفحة الخطوات المعلقة للتأكيد
        public async Task<IActionResult> PendingApprovals()
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

            // التحقق من صلاحية مؤكد الخطوات
            if (user == null || !user.IsStepApprover)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية الوصول لهذه الصفحة";
                return RedirectToAction("Index", "Home");
            }

            var pendingSteps = await _db.Steps
                .Where(s => !s.IsDeleted && s.ApprovalStatus == ApprovalStatus.Pending)
                .Include(s => s.Project)
                    .ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo)
                .Include(s => s.Attachments)
                .OrderBy(s => s.SubmittedForApprovalAt)
                .ToListAsync();

            ViewBag.PendingCount = pendingSteps.Count;

            return View(pendingSteps);
        }

        // GET: /Steps/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                    .ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo)
                .Include(s => s.CreatedBy)
                .Include(s => s.DependsOnStep)
                .Include(s => s.ApprovedBy)
                .Include(s => s.Attachments)
                    .ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanAccessStep(step))
            {
                return Forbid();
            }

            UpdateStepDelayedStatus(step);

            var viewModel = new StepDetailsViewModel
            {
                Step = step,
                Notes = await _db.ProgressUpdates
                    .Where(p => p.StepId == id)
                    .Include(p => p.CreatedBy)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(20)
                    .ToListAsync()
            };

            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

            ViewBag.CanEdit = CanEditProject(step.Project);
            ViewBag.UserRole = GetCurrentUserRole();
            ViewBag.IsStepApprover = user?.IsStepApprover ?? false;

            return View(viewModel);
        }

        // GET: /Steps/Create?projectId=5
        public async Task<IActionResult> Create(int? projectId)
        {
            if (!projectId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب تحديد المشروع لإضافة خطوة";
                return RedirectToAction("Index", "Projects");
            }

            var project = await _db.Projects
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId.Value && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }

            if (!CanEditProject(project))
            {
                return Forbid();
            }

            var usedWeight = project.Steps.Sum(s => s.Weight);
            var remainingWeight = 100 - usedWeight;

            var viewModel = new StepFormViewModel
            {
                ProjectId = projectId.Value,
                ProjectName = project.NameAr,
                Weight = remainingWeight > 0 ? Math.Min(remainingWeight, 10) : 10
            };

            var lastStepNumber = project.Steps.Any()
                ? project.Steps.Max(s => s.StepNumber)
                : 0;
            viewModel.StepNumber = lastStepNumber + 1;

            await PopulateFormDropdowns(viewModel);

            ViewBag.ProjectName = project.NameAr;
            ViewBag.InitiativeName = project.Initiative?.NameAr;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;

            return View(viewModel);
        }

        // POST: /Steps/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StepFormViewModel model)
        {
            var project = await _db.Projects
                .Include(p => p.Initiative)
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.IsDeleted);

            if (project == null)
            {
                return NotFound();
            }

            if (!CanEditProject(project))
            {
                return Forbid();
            }

            var usedWeight = project.Steps.Sum(s => s.Weight);
            var remainingWeight = 100 - usedWeight;

            if (model.Weight > remainingWeight)
            {
                ModelState.AddModelError("Weight", $"الوزن المتبقي للمشروع هو {remainingWeight}% فقط");
            }

            if (ModelState.IsValid)
            {
                var step = new Step
                {
                    CreatedById = GetCurrentUserId(),
                    CreatedAt = DateTime.Now,
                    InitiativeId = project.InitiativeId,
                    Status = StepStatus.NotStarted,
                    ApprovalStatus = ApprovalStatus.None

                };

                model.UpdateEntity(step);
                // ربط المعيّن إليه بـ User في DB عبر ADUsername
                step.AssignedToId = await ResolveUserIdByEmpNumber(model.AssignedToEmpNumber);

                _db.Steps.Add(step);
                await _db.SaveChangesAsync();

                await UpdateProjectProgressAsync(project.Id);

                TempData["SuccessMessage"] = "تم إضافة الخطوة بنجاح";
                return RedirectToAction("Details", "Projects", new { id = step.ProjectId });
            }

            await PopulateFormDropdowns(model);
            ViewBag.ProjectName = project.NameAr;
            ViewBag.InitiativeName = project.Initiative?.NameAr;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;

            return View(model);
        }

        // GET: /Steps/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                    .ThenInclude(p => p.Initiative)
                .Include(s => s.Project)
                    .ThenInclude(p => p.Steps.Where(st => !st.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanEditProject(step.Project))
            {
                return Forbid();
            }

            var viewModel = StepFormViewModel.FromEntity(step);
            await PopulateFormDropdowns(viewModel);

            var usedWeight = step.Project.Steps.Where(s => s.Id != id).Sum(s => s.Weight);
            var remainingWeight = 100 - usedWeight;

            ViewBag.ProjectName = step.Project?.NameAr;
            ViewBag.InitiativeName = step.Project?.Initiative?.NameAr;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;

            return View(viewModel);
        }

        // POST: /Steps/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StepFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var step = await _db.Steps
                .Include(s => s.Project)
                    .ThenInclude(p => p.Steps.Where(st => !st.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanEditProject(step.Project))
            {
                return Forbid();
            }

            var usedWeight = step.Project.Steps.Where(s => s.Id != id).Sum(s => s.Weight);
            var remainingWeight = 100 - usedWeight;

            if (model.Weight > remainingWeight)
            {
                ModelState.AddModelError("Weight", $"الوزن المتبقي للمشروع هو {remainingWeight}% فقط");
            }

            if (ModelState.IsValid)
            {
                model.UpdateEntity(step);
                // ربط المعيّن إليه بـ User في DB عبر ADUsername
                step.AssignedToId = await ResolveUserIdByEmpNumber(model.AssignedToEmpNumber);
                step.LastModifiedById = GetCurrentUserId();
                step.LastModifiedAt = DateTime.Now;

                UpdateStepDelayedStatus(step);

                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تحديث الخطوة بنجاح";
                return RedirectToAction(nameof(Details), new { id = step.Id });
            }

            var project = await _db.Projects
                .Include(p => p.Initiative)
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

            await PopulateFormDropdowns(model);
            ViewBag.ProjectName = project?.NameAr;
            ViewBag.InitiativeName = project?.Initiative?.NameAr;
            ViewBag.UsedWeight = usedWeight;
            ViewBag.RemainingWeight = remainingWeight;

            return View(model);
        }

        // GET: /Steps/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                    .ThenInclude(p => p.Initiative)
                .Include(s => s.AssignedTo)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanEditProject(step.Project))
            {
                return Forbid();
            }

            return View(step);
        }

        // POST: /Steps/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanEditProject(step.Project))
            {
                return Forbid();
            }

            var hasDependents = await _db.Steps
                .AnyAsync(s => s.DependsOnStepId == id && !s.IsDeleted);

            if (hasDependents)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف هذه الخطوة لأن هناك خطوات أخرى تعتمد عليها";
                return RedirectToAction(nameof(Delete), new { id });
            }

            var projectId = step.ProjectId;

            step.IsDeleted = true;
            step.LastModifiedById = GetCurrentUserId();
            step.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            await UpdateProjectProgressAsync(projectId);

            TempData["SuccessMessage"] = "تم حذف الخطوة بنجاح";
            return RedirectToAction("Details", "Projects", new { id = projectId });
        }

        // POST: /Steps/UpdateProgress/5 - تحديث النسبة فقط (بدون إكمال)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(int id, decimal progress, string? notes)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanAccessStep(step))
            {
                return Forbid();
            }

            // لا يمكن تعديل خطوة معلقة للتأكيد
            if (step.ApprovalStatus == ApprovalStatus.Pending)
            {
                TempData["ErrorMessage"] = "لا يمكن تعديل خطوة معلقة للتأكيد";
                return RedirectToAction(nameof(Details), new { id });
            }

            // إذا كانت النسبة 100% يجب إرسالها للتأكيد
            if (progress >= 100)
            {
                TempData["ErrorMessage"] = "لإكمال الخطوة استخدم زر 'إرسال للتأكيد' مع إرفاق ملف التوثيق";
                return RedirectToAction(nameof(Details), new { id });
            }

            var progressUpdate = new ProgressUpdate
            {
                StepId = id,
                PreviousPercentage = step.ProgressPercentage,
                ProgressPercentage = progress,
                NotesAr = notes,
                UpdateType = UpdateType.StatusChange,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            step.ProgressPercentage = progress;
            step.LastModifiedById = GetCurrentUserId();
            step.LastModifiedAt = DateTime.Now;

            if (progress > 0)
            {
                step.Status = StepStatus.InProgress;
                step.ActualStartDate ??= DateTime.Today;
            }

            UpdateStepDelayedStatus(step);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تحديث نسبة الإنجاز بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Steps/SubmitForApproval/5 - إرسال للتأكيد مع مرفق
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id, string completionDetails, IFormFile attachmentFile)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanAccessStep(step))
            {
                return Forbid();
            }

            // التحقق من أن الخطوة ليست معلقة بالفعل
            if (step.ApprovalStatus == ApprovalStatus.Pending)
            {
                TempData["ErrorMessage"] = "الخطوة معلقة للتأكيد بالفعل";
                return RedirectToAction(nameof(Details), new { id });
            }

            // المرفق اختياري — نتحقق منه فقط إذا أُرفق
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(attachmentFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["ErrorMessage"] = "نوع الملف غير مسموح. الأنواع المسموحة: PDF, JPG, PNG";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (attachmentFile.Length > 10 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "حجم الملف يجب أن يكون أقل من 10 ميجابايت";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // حفظ الملف
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "steps", id.ToString());
                Directory.CreateDirectory(uploadsFolder);

                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var uniqueFileName = $"S{id}_{timestamp}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await attachmentFile.CopyToAsync(stream);
                }

                var attachment = new StepAttachment
                {
                    StepId = id,
                    FileName = uniqueFileName,
                    OriginalFileName = attachmentFile.FileName,
                    ContentType = attachmentFile.ContentType,
                    FileSize = attachmentFile.Length,
                    FilePath = $"/uploads/steps/{id}/{uniqueFileName}",
                    Description = "مرفق توثيق إتمام الخطوة",
                    UploadedById = GetCurrentUserId(),
                    UploadedAt = DateTime.Now
                };
                _db.StepAttachments.Add(attachment);
            }

            // تحديث الخطوة
            step.ProgressPercentage = 100;
            step.Status = StepStatus.InProgress; // تبقى InProgress حتى التأكيد
            step.ApprovalStatus = ApprovalStatus.Pending;
            step.CompletionDetails = completionDetails;
            step.SubmittedForApprovalAt = DateTime.Now;
            step.RejectionReason = null; // مسح سبب الرفض السابق إن وجد
            step.LastModifiedById = GetCurrentUserId();
            step.LastModifiedAt = DateTime.Now;
            step.ActualStartDate ??= DateTime.Today;

            // سجل التحديث
            var progressUpdate = new ProgressUpdate
            {
                StepId = id,
                PreviousPercentage = step.ProgressPercentage,
                ProgressPercentage = 100,
                NotesAr = "تم إرسال الخطوة للتأكيد",
                UpdateType = UpdateType.StatusChange,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إرسال الخطوة للتأكيد بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStep(int id, string? approverNotes)
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

            if (user == null || !user.IsStepApprover)
            {
                return Forbid();
            }

            var step = await _db.Steps
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null) return NotFound();

            if (step.ApprovalStatus != ApprovalStatus.Pending)
            {
                TempData["ErrorMessage"] = "الخطوة ليست معلقة للتأكيد";
                return RedirectToAction(nameof(Details), new { id });
            }

            step.ApprovalStatus = ApprovalStatus.Approved;
            step.Status = StepStatus.Completed;
            step.ApprovedById = userId;
            step.ApprovedAt = DateTime.Now;
            step.ActualEndDate = DateTime.Today;
            step.ApproverNotes = approverNotes;
            step.LastModifiedById = userId;
            step.LastModifiedAt = DateTime.Now;

            var progressUpdate = new ProgressUpdate
            {
                StepId = id,
                ProgressPercentage = 100,
                NotesAr = string.IsNullOrWhiteSpace(approverNotes)
                    ? "تم تأكيد إكمال الخطوة"
                    : $"تم تأكيد إكمال الخطوة - ملاحظة المؤكد: {approverNotes}",
                UpdateType = UpdateType.StatusChange,
                CreatedById = userId,
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            await _db.SaveChangesAsync();
            await UpdateProjectProgressAsync(step.ProjectId);

            TempData["SuccessMessage"] = "تم تأكيد الخطوة بنجاح وتم احتساب الوزن في المشروع";
            return RedirectToAction(nameof(PendingApprovals));
        }

        // POST: /Steps/RejectStep/5 - رفض الخطوة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStep(int id, string rejectionReason)
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);

            if (user == null || !user.IsStepApprover)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "يجب كتابة سبب الرفض";
                return RedirectToAction(nameof(Details), new { id });
            }

            var step = await _db.Steps
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (step.ApprovalStatus != ApprovalStatus.Pending)
            {
                TempData["ErrorMessage"] = "الخطوة ليست معلقة للتأكيد";
                return RedirectToAction(nameof(Details), new { id });
            }

            // رفض الخطوة
            step.ApprovalStatus = ApprovalStatus.Rejected;
            step.Status = StepStatus.InProgress;
            step.ProgressPercentage = 99; // ترجع لأقل من 100
            step.RejectionReason = rejectionReason;
            step.LastModifiedById = userId;
            step.LastModifiedAt = DateTime.Now;

            // سجل الرفض
            var progressUpdate = new ProgressUpdate
            {
                StepId = id,
                ProgressPercentage = 99,
                NotesAr = $"تم رفض الخطوة - السبب: {rejectionReason}",
                UpdateType = UpdateType.StatusChange,
                CreatedById = userId,
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم رفض الخطوة وإرسالها للمسؤول للتعديل";
            return RedirectToAction(nameof(PendingApprovals));
        }

        // POST: /Steps/AddNote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string notes)
        {
            var step = await _db.Steps
                .Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (step == null)
            {
                return NotFound();
            }

            if (!CanAccessStep(step))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id });
            }

            var progressUpdate = new ProgressUpdate
            {
                StepId = id,
                NotesAr = notes,
                UpdateType = UpdateType.Note,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            };
            _db.ProgressUpdates.Add(progressUpdate);

            step.LastModifiedById = GetCurrentUserId();
            step.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Steps/EditNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int noteId, int stepId, string notes)
        {
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.StepId != stepId)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["ErrorMessage"] = "الملاحظة مطلوبة";
                return RedirectToAction(nameof(Details), new { id = stepId });
            }

            note.NotesAr = notes;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعديل الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = stepId });
        }

        // POST: /Steps/DeleteNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int noteId, int stepId)
        {
            if (GetCurrentUserRole() != UserRole.Admin)
            {
                return Forbid();
            }

            var note = await _db.ProgressUpdates.FindAsync(noteId);
            if (note == null || note.StepId != stepId)
            {
                return NotFound();
            }

            _db.ProgressUpdates.Remove(note);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الملاحظة بنجاح";
            return RedirectToAction(nameof(Details), new { id = stepId });
        }

        // POST: /Steps/MarkComplete/5 - إعادة توجيه لصفحة الإرسال للتأكيد
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkComplete(int id)
        {
            TempData["InfoMessage"] = "لإكمال الخطوة، يرجى استخدام نموذج 'إرسال للتأكيد' مع إرفاق ملف التوثيق";
            return RedirectToAction(nameof(Details), new { id });
        }

        #region Helper Methods

        /// <summary>
        /// يبحث عن User.Id بناءً على EmpNumber (ADUsername)
        /// </summary>
        private async Task<int?> ResolveUserIdByEmpNumber(string? empNumber)
        {
            if (string.IsNullOrWhiteSpace(empNumber)) return null;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.ADUsername == empNumber && u.IsActive);
            return user?.Id;
        }

        private void UpdateStepDelayedStatus(Step step)
        {
            if (step.Status == StepStatus.Completed || step.Status == StepStatus.Cancelled)
                return;

            if (step.ActualEndDate.HasValue &&
                step.ActualEndDate.Value < DateTime.Today &&
                step.ProgressPercentage < 100)
            {
                step.Status = StepStatus.Delayed;
            }
        }

        /// <summary>
        /// تحديث نسبة إنجاز المشروع - تحسب فقط الخطوات المؤكدة
        /// </summary>
        private async Task UpdateProjectProgressAsync(int projectId)
        {
            var project = await _db.Projects
                .Include(p => p.Steps.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

            if (project == null) return;

            // مجموع أوزان الخطوات المؤكدة فقط
            var completedWeight = project.Steps
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved && s.ProgressPercentage >= 100)
                .Sum(s => s.Weight);

            project.ProgressPercentage = completedWeight;
            await _db.SaveChangesAsync();
        }

        private bool CanAccessStep(Step step)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Admin || userRole == UserRole.Executive)
                return true;

            var project = step.Project ?? _db.Projects.Include(p => p.Initiative).FirstOrDefault(p => p.Id == step.ProjectId);
            if (project == null) return false;

            return userRole switch
            {
                UserRole.Supervisor => project.Initiative?.SupervisorId == userId,
                UserRole.User => project.ProjectManagerId == userId,
                UserRole.StepUser => step.AssignedToId == userId,
                _ => false
            };
        }

        private bool CanEditProject(Project project)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (userRole == UserRole.Admin)
                return true;

            if (userRole == UserRole.Supervisor)
            {
                var initiative = project.Initiative ?? _db.Initiatives.FirstOrDefault(i => i.Id == project.InitiativeId);
                return initiative?.SupervisorId == userId;
            }

            if (userRole == UserRole.User && project.ProjectManagerId == userId)
                return true;

            // StepUser لا يستطيع إضافة/حذف خطوات، فقط تحديث نسبة الإنجاز
            return false;
        }

        private async Task PopulateFilterDropdowns(StepListViewModel model)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var projectsQuery = _db.Projects.Where(p => !p.IsDeleted);

            if (userRole == UserRole.Supervisor)
            {
                var supervisedInitiativeIds = await _db.Initiatives
                    .Where(i => i.SupervisorId == userId && !i.IsDeleted)
                    .Select(i => i.Id)
                    .ToListAsync();
                projectsQuery = projectsQuery.Where(p => supervisedInitiativeIds.Contains(p.InitiativeId));
            }
            else if (userRole == UserRole.User)
            {
                projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == userId);
            }

            model.Projects = new SelectList(
                await projectsQuery.OrderBy(p => p.NameAr).ToListAsync(),
                "Id", "NameAr", model.ProjectId);

            model.Statuses = new SelectList(
                Enum.GetValues<StepStatus>().Select(s => new {
                    Value = (int)s,
                    Text = GetStepStatusArabicName(s)
                }),
                "Value", "Text", model.StatusFilter);
        }

        private async Task PopulateFormDropdowns(StepFormViewModel model)
        {
            model.Users = new SelectList(
                await _db.Users.Where(u => u.IsActive).ToListAsync(),
                "Id", "FullNameAr", model.AssignedToId);

            var otherSteps = await _db.Steps
                .Where(s => !s.IsDeleted && s.Id != model.Id && s.ProjectId == model.ProjectId)
                .OrderBy(s => s.StepNumber)
                .Select(s => new { s.Id, Name = s.StepNumber + " - " + s.NameAr })
                .ToListAsync();

            model.DependsOnSteps = new SelectList(otherSteps, "Id", "Name", model.DependsOnStepId);
        }

        private string GetStepStatusArabicName(StepStatus status)
        {
            return status switch
            {
                StepStatus.NotStarted => "لم تبدأ",
                StepStatus.InProgress => "جارية",
                StepStatus.Completed => "مكتملة",
                StepStatus.Delayed => "متأخرة",
                StepStatus.Cancelled => "ملغاة",
                StepStatus.OnHold => "معلقة",
                _ => status.ToString()
            };
        }

        #endregion
    }
}