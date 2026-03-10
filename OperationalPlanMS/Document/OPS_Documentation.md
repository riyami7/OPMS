# 📋 توثيق نظام إدارة الخطط التشغيلية (OPS)
## Operational Plan Management System

---

## 📌 معلومات عامة

| البند | التفاصيل |
|-------|----------|
| **اسم النظام** | Operational Plan Management System (OPS) |
| **التقنية** | ASP.NET Core MVC 8.0 |
| **قاعدة البيانات** | SQL Server (localhost/OperationalPlanMS) |
| **المصادقة** | Cookie Authentication |
| **بيانات الدخول** | admin / admin123 |
| **المسار** | `C:\Users\MSI-Laptop\source\repos\OperationalPlanMS\OperationalPlanMS\` |

---

## 🏗️ هيكل النظام

```
OperationalPlanMS/
├── Controllers/
│   ├── BaseController.cs          ← Controller أساسي مشترك
│   ├── AccountController.cs       ← تسجيل الدخول/الخروج
│   ├── AdminController.cs         ← لوحة الإدارة
│   ├── DashboardController.cs     ← الصفحة الرئيسية
│   ├── InitiativesController.cs   ← إدارة المبادرات
│   ├── ProjectsController.cs      ← إدارة المشاريع
│   ├── StepsController.cs         ← إدارة الخطوات
│   ├── ReportsController.cs       ← التقارير
│   ├── UsersController.cs         ← إدارة المستخدمين
│   ├── OrganizationsController.cs ← إدارة المنظمات
│   └── FiscalYearsController.cs   ← إدارة السنوات المالية
├── Models/
│   ├── Enums.cs                   ← التعدادات
│   ├── Entities/                  ← كيانات قاعدة البيانات
│   └── ViewModels/                ← نماذج العرض
├── Views/
│   ├── Shared/                    ← القوالب المشتركة
│   ├── Account/                   ← صفحات المصادقة
│   ├── Admin/                     ← لوحة الإدارة
│   ├── Dashboard/                 ← الصفحة الرئيسية
│   ├── Initiatives/               ← صفحات المبادرات
│   ├── Projects/                  ← صفحات المشاريع
│   ├── Steps/                     ← صفحات الخطوات
│   └── Reports/                   ← صفحات التقارير
├── Data/
│   └── AppDbContext.cs            ← سياق قاعدة البيانات
└── wwwroot/
    ├── css/
    ├── js/
    └── lib/
```

---

## 🗄️ هيكل قاعدة البيانات

### العلاقات الرئيسية

```
Organization (المنظمة)
    │
    ├── OrganizationalUnit (الوحدة التنظيمية) [Flat Structure - ParentId = null]
    │       │
    │       └── Initiative (المبادرة)
    │               │
    │               └── Project (المشروع)
    │                       │
    │                       └── Step (الخطوة)
    │
    ├── FiscalYear (السنة المالية)
    │
    └── User (المستخدم)
```

### الجداول

#### 1. Organizations (المنظمات)
```sql
- Id (PK)
- Code (unique)
- NameAr, NameEn
- DescriptionAr, DescriptionEn
- IsActive
- CreatedAt, CreatedById
```

#### 2. OrganizationalUnits (الوحدات التنظيمية)
```sql
- Id (PK)
- Code (unique)
- NameAr, NameEn
- OrganizationId (FK)
- ParentId (FK, nullable) ← دائماً null (Flat Structure)
- ManagerId (FK)
- IsActive
- CreatedAt, CreatedById
```

#### 3. FiscalYears (السنوات المالية)
```sql
- Id (PK)
- Year
- NameAr, NameEn
- StartDate, EndDate
- IsCurrent
- OrganizationId (FK) ← مرتبط بالمنظمة
- CreatedAt, CreatedBy
```

#### 4. Users (المستخدمين)
```sql
- Id (PK)
- Username (unique)
- PasswordHash
- FullNameAr, FullNameEn
- Email, Phone
- Role (enum: Admin=1, Supervisor=2, User=3, Viewer=4)
- OrganizationId (FK)
- OrganizationalUnitId (FK, nullable)
- IsActive
- CreatedAt, LastLoginAt
```

#### 5. Initiatives (المبادرات)
```sql
- Id (PK)
- Code (auto-generated: INI-YYYY-XXXX)
- NameAr, NameEn
- DescriptionAr, DescriptionEn
- FiscalYearId (FK)
- OrganizationalUnitId (FK)
- SupervisorId (FK) ← المشرف على المبادرة
- StrategicObjectiveId (FK, nullable)
- ActualStartDate, ActualEndDate ← التواريخ الفعلية فقط
- Budget, ActualCost
- IsDeleted (soft delete)
- CreatedAt, CreatedById
- LastModifiedAt, LastModifiedById

⚠️ الحقول الملغاة من الواجهة (تبقى في DB):
- Status, Priority, Weight, ProgressPercentage
- PlannedStartDate, PlannedEndDate
```

#### 6. Projects (المشاريع)
```sql
- Id (PK)
- Code (auto-generated: PRJ-YYYY-XXXX)
- NameAr, NameEn
- DescriptionAr, DescriptionEn
- InitiativeId (FK)
- OrganizationalUnitId (FK)
- ProjectManagerId (FK)
- ActualStartDate, ActualEndDate
- Budget, ActualCost
- ProgressPercentage ← يُحسب تلقائياً من الخطوات
- ExpectedOutcomes, KPIs, RiskNotes
- IsDeleted
- CreatedAt, CreatedById
- LastModifiedAt, LastModifiedById

⚠️ الحقول الملغاة من الواجهة:
- Status, Priority, Weight
- PlannedStartDate, PlannedEndDate
```

#### 7. Steps (الخطوات)
```sql
- Id (PK)
- StepNumber (ترتيب داخل المشروع)
- NameAr, NameEn
- DescriptionAr, DescriptionEn
- ProjectId (FK)
- AssignedToId (FK)
- Weight ← وزن الخطوة (المجموع = 100%)
- ProgressPercentage (0-100)
- ActualStartDate, ActualEndDate
- Status ← يُحسب تلقائياً
- IsDeleted
- CreatedAt, CreatedById
- LastModifiedAt, LastModifiedById

⚠️ الحقول الملغاة:
- PlannedStartDate, PlannedEndDate
```

#### 8. ProgressUpdates (الملاحظات/التحديثات)
```sql
- Id (PK)
- InitiativeId, ProjectId, StepId (FKs, nullable)
- UpdateType (enum: Progress, StatusChange, Note, Milestone)
- PreviousValue, NewValue
- NotesAr, NotesEn
- CreatedAt, CreatedById
```

---

## 🔐 نظام الصلاحيات (RBAC)

### الأدوار (Roles)

```csharp
public enum UserRole
{
    Admin = 1,      // مدير النظام
    Supervisor = 2, // مشرف
    User = 3,       // مستخدم عادي
    Viewer = 4      // مشاهد فقط
}
```

### صلاحيات كل دور

| الصلاحية | Admin | Supervisor | User | Viewer |
|----------|-------|------------|------|--------|
| إدارة المستخدمين | ✅ | ❌ | ❌ | ❌ |
| إدارة المنظمات | ✅ | ❌ | ❌ | ❌ |
| إدارة السنوات المالية | ✅ | ❌ | ❌ | ❌ |
| إنشاء مبادرات | ✅ | ✅ | ❌ | ❌ |
| تعديل مبادراته | ✅ | ✅ | ❌ | ❌ |
| إنشاء مشاريع | ✅ | ✅ | ❌ | ❌ |
| تعديل مشاريعه | ✅ | ✅ | ✅ | ❌ |
| إنشاء/تعديل خطوات | ✅ | ✅ | ✅ | ❌ |
| عرض البيانات | ✅ | ✅ | ✅ | ✅ |

### قواعد الوصول

```csharp
// Admin: يرى كل شيء
// Supervisor: يرى مبادراته فقط (SupervisorId == userId)
// User: يرى مشاريعه فقط (ProjectManagerId == userId)
// Viewer: يرى كل شيء (قراءة فقط)
```

---

## 📊 آلية حساب نسبة الإنجاز

### المبدأ الأساسي

```
┌─────────────────────────────────────────────────────────────┐
│  نسبة المشروع = مجموع أوزان الخطوات المكتملة 100% فقط      │
└─────────────────────────────────────────────────────────────┘
```

### مثال تفصيلي

```
مشروع به 4 خطوات:

خطوة 1: وزن 25%، إنجاز 100% ✓ → تُحسب (25%)
خطوة 2: وزن 25%، إنجاز 80%  ✗ → لا تُحسب
خطوة 3: وزن 30%، إنجاز 100% ✓ → تُحسب (30%)
خطوة 4: وزن 20%، إنجاز 0%   ✗ → لا تُحسب
─────────────────────────────────────────────
نسبة المشروع = 25% + 30% = 55%
```

### الكود

```csharp
// حساب نسبة المشروع
project.ProgressPercentage = project.Steps
    .Where(s => !s.IsDeleted && s.ProgressPercentage >= 100)
    .Sum(s => s.Weight);
```

### حالة الخطوة التلقائية

```csharp
// تُحسب تلقائياً:
if (ProgressPercentage >= 100) → "مكتمل" ✅
else if (ActualEndDate < Today && Progress < 100) → "متأخر" ⚠️
else if (ProgressPercentage > 0) → "قيد التنفيذ" 🔄
else → "لم يبدأ" ⏳
```

### التحقق من الأوزان

```
⚠️ مجموع أوزان الخطوات يجب = 100%
- يظهر تنبيه إذا كان المجموع ≠ 100%
- لا يمنع الحفظ (تحذير فقط)
```

---

## 🏷️ التعدادات (Enums)

```csharp
// Models/Enums.cs

public enum UserRole
{
    Admin = 1,
    Supervisor = 2,
    User = 3,
    Viewer = 4
}

public enum Status  // للتوافقية - لا تُستخدم في الواجهة
{
    Draft = 1,
    Pending = 2,
    Approved = 3,
    InProgress = 4,
    OnHold = 5,
    Completed = 6,
    Cancelled = 7,
    Delayed = 8
}

public enum StepStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    OnHold = 4,
    Cancelled = 5,
    Delayed = 6  // ← تُحسب تلقائياً
}

public enum Priority  // للتوافقية - لا تُستخدم
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum UpdateType
{
    Progress = 1,
    StatusChange = 2,
    Note = 3,       // ← للملاحظات
    Milestone = 4
}
```

---

## 🔄 توليد الأكواد التلقائي

### صيغة الكود

```
المبادرة: INI-YYYY-XXXX
المشروع: PRJ-YYYY-XXXX
الخطوة: رقم تسلسلي داخل المشروع
```

### الكود

```csharp
// توليد كود المبادرة
private async Task<string> GenerateInitiativeCode()
{
    var year = DateTime.Now.Year;
    var count = await _db.Initiatives
        .CountAsync(i => i.CreatedAt.Year == year) + 1;
    return $"INI-{year}-{count:D4}";
}

// توليد كود المشروع
private async Task<string> GenerateProjectCode()
{
    var year = DateTime.Now.Year;
    var count = await _db.Projects
        .CountAsync(p => p.CreatedAt.Year == year) + 1;
    return $"PRJ-{year}-{count:D4}";
}
```

---

## 📝 الملاحظات (Notes System)

### كيف تعمل

- الملاحظات تُخزن في جدول `ProgressUpdates`
- `UpdateType = Note`
- يمكن إضافتها من صفحة Details لأي عنصر

### الكود

```csharp
// إضافة ملاحظة للمشروع
[HttpPost]
public async Task<IActionResult> AddNote(int id, string notes)
{
    var update = new ProgressUpdate
    {
        ProjectId = id,
        UpdateType = UpdateType.Note,
        NotesAr = notes,
        CreatedAt = DateTime.Now,
        CreatedById = GetCurrentUserId()
    };
    _db.ProgressUpdates.Add(update);
    await _db.SaveChangesAsync();
    return RedirectToAction(nameof(Details), new { id });
}
```

---

## 🎨 واجهة المستخدم

### المكتبات المستخدمة

| المكتبة | الاستخدام |
|---------|-----------|
| Bootstrap 5 | التصميم الأساسي |
| Bootstrap Icons | الأيقونات |
| DataTables | جداول البيانات |
| Chart.js | الرسوم البيانية |
| Tajawal Font | الخط العربي |
| Inter Font | الخط الإنجليزي |

### الألوان الرئيسية

```css
--primary: #667eea;
--success: #10b981;
--warning: #f59e0b;
--danger: #ef4444;
--info: #06b6d4;
```

### Badge Classes

```css
.badge-completed { background: #10b981; }  /* أخضر */
.badge-inprogress { background: #06b6d4; } /* أزرق */
.badge-delayed { background: #ef4444; }    /* أحمر */
.badge-draft { background: #6b7280; }      /* رمادي */
```

---

## 📄 الصفحات الرئيسية

### المبادرات (Initiatives)

| الصفحة | الوظيفة |
|--------|---------|
| Index | قائمة المبادرات مع فلاتر |
| Create | إنشاء مبادرة جديدة |
| Edit | تعديل مبادرة |
| Details | عرض تفاصيل + المشاريع + الملاحظات |
| Delete | حذف مبادرة |

### المشاريع (Projects)

| الصفحة | الوظيفة |
|--------|---------|
| Index | قائمة المشاريع |
| Create | إنشاء مشروع (من صفحة المبادرة) |
| Edit | تعديل + عرض نسبة الإنجاز المحسوبة |
| Details | عرض تفاصيل + جدول الخطوات بالأوزان + الملاحظات |
| Delete | حذف مشروع |

### الخطوات (Steps)

| الصفحة | الوظيفة |
|--------|---------|
| Index | قائمة الخطوات |
| Create | إنشاء خطوة مع تحديد الوزن |
| Edit | تعديل + تحديث نسبة الإنجاز |
| Details | عرض تفاصيل + حالة تلقائية + الملاحظات |
| Delete | حذف خطوة |

### التقارير (Reports)

| القسم | المحتوى |
|-------|---------|
| Summary Stats | إجمالي المبادرات/المشاريع/الخطوات |
| Status Chart | توزيع الحالات (Doughnut) |
| Progress Chart | التقدم الشهري (Bar) |
| Budget Overview | ملخص الميزانية |
| Top Performers | أفضل المبادرات |
| Needs Attention | المشاريع والخطوات المتأخرة |
| By Unit | ملخص حسب الوحدة التنظيمية |

---

## ⚠️ التغييرات الأخيرة (يناير 2026)

### ما تم إلغاؤه من الواجهة

| الكيان | الحقول الملغاة |
|--------|----------------|
| Initiative | Status, Priority, Weight, ProgressPercentage, PlannedStartDate, PlannedEndDate |
| Project | Status, Priority, Weight, PlannedStartDate, PlannedEndDate |
| Step | PlannedStartDate, PlannedEndDate, Status (يُحسب تلقائياً) |

### ما تم إضافته

| الميزة | الوصف |
|--------|-------|
| Weight للخطوات | كل خطوة لها وزن (المجموع = 100%) |
| حساب تلقائي | نسبة المشروع = مجموع أوزان الخطوات المكتملة |
| حالة تلقائية | الخطوة تصبح "متأخرة" إذا تجاوزت ActualEndDate |
| نظام الملاحظات | إضافة ملاحظات لأي عنصر |
| Weight Validation | تنبيه إذا مجموع الأوزان ≠ 100% |

---

## 🔧 BaseController

```csharp
public class BaseController : Controller
{
    // الحصول على معرف المستخدم الحالي
    protected int GetCurrentUserId()
    {
        var claim = User.FindFirst("UserId");
        return claim != null ? int.Parse(claim.Value) : 0;
    }

    // الحصول على دور المستخدم الحالي
    protected UserRole GetCurrentUserRole()
    {
        var claim = User.FindFirst("Role");
        if (claim != null && Enum.TryParse<UserRole>(claim.Value, out var role))
            return role;
        return UserRole.Viewer;
    }

    // التحقق من صلاحية التعديل
    protected bool CanEdit()
    {
        var role = GetCurrentUserRole();
        return role == UserRole.Admin || 
               role == UserRole.Supervisor || 
               role == UserRole.User;
    }

    // ترجمة الحالات
    protected string GetStatusArabicName(Status status) { ... }
    protected string GetStepStatusArabicName(StepStatus status) { ... }
}
```

---

## 🚀 تشغيل المشروع

```powershell
cd "C:\Users\MSI-Laptop\source\repos\OperationalPlanMS\OperationalPlanMS"
dotnet build
dotnet run
```

**URL:** https://localhost:5001 أو http://localhost:5000

---

## 📋 ملاحظات مهمة للمطور

1. **Soft Delete:** جميع الحذف يستخدم `IsDeleted = true`
2. **لا Migration:** التغييرات لا تتطلب migration (الحقول تبقى في DB)
3. **التواريخ:** فقط `ActualStartDate` و `ActualEndDate` تُستخدم
4. **الأوزان:** يجب أن يساوي مجموعها 100% (تحذير فقط)
5. **الملاحظات:** تُخزن في `ProgressUpdates` مع `UpdateType.Note`
6. **RTL:** الواجهة تدعم العربية (RTL)

---

## 📞 للتواصل

- **المشروع:** OmanAI
- **التاريخ:** يناير 2026

---

*آخر تحديث: 19 يناير 2026*
