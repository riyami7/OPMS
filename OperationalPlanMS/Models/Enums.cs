namespace OperationalPlanMS.Models
{
    /// <summary>
    /// User roles for authorization
    /// </summary>
    public enum UserRole
    {
        Admin = 1,      // مدير النظام
        Executive = 2,  // التنفيذي
        Supervisor = 3, // المشرف
        User = 4,       // مدير المشروع
        StepUser = 7    // منفذ الخطوة
    }

    /// <summary>
    /// Status for Initiatives and Projects (للتوافق مع DB - لم يعد مستخدماً في الواجهة)
    /// </summary>
    public enum Status
    {
        Draft = 0,
        Pending = 1,
        Approved = 2,
        InProgress = 3,
        OnHold = 4,
        Completed = 5,
        Cancelled = 6,
        Delayed = 7
    }

    /// <summary>
    /// Status for Steps
    /// </summary>
    public enum StepStatus
    {
        NotStarted = 0, // لم تبدأ
        InProgress = 1, // جارية
        Completed = 2,  // مكتملة
        OnHold = 3,     // متوقفة
        Cancelled = 4,  // ملغاة
        Delayed = 5     // متأخرة ← جديد (تلقائي عند تجاوز التاريخ)
    }

    /// <summary>
    /// Priority levels (للتوافق مع DB - لم يعد مستخدماً في الواجهة)
    /// </summary>
    public enum Priority
    {
        Highest = 1,
        High = 2,
        Medium = 3,
        Low = 4,
        Lowest = 5
    }

    /// <summary>
    /// Milestone types (مجمد حالياً)
    /// </summary>
    public enum MilestoneType
    {
        Checkpoint = 0,
        Review = 1,
        Approval = 2,
        Testing = 3,
        Delivery = 4
    }

    /// <summary>
    /// Progress update types
    /// </summary>
    public enum UpdateType
    {
        Regular = 0,    // تحديث عادي
        Note = 1,       // ملاحظة
        StatusChange = 2 // تغيير حالة
    }

    /// <summary>
    /// Document categories
    /// </summary>
    public enum DocumentCategory
    {
        General = 0,
        Report = 1,
        Evidence = 2,
        Attachment = 3
    }
}