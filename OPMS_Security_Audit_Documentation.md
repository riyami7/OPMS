# OPMS Security Audit — توثيق الفحص والإصلاح الأمني

> **المشروع:** OPMS — نظام إدارة الخطط التشغيلية  
> **التاريخ:** 12 مارس 2026  
> **البرانش:** `feature/security-fixes` (مدمج في `master`)  
> **الملفات المعدلة:** 10 ملفات | 315 إضافة | 39 حذف  
> **Migration مطلوب:** لا

---

## الفهرس

1. [ملخص الفحص الأمني](#1-ملخص-الفحص-الأمني)
2. [المراحل المنفذة](#2-المراحل-المنفذة)
3. [الملفات المعدلة](#3-الملفات-المعدلة)
4. [نقاط المرجعية (Backup Tags)](#4-نقاط-المرجعية-backup-tags)
5. [أوامر الرجوع للنسخ السابقة](#5-أوامر-الرجوع-للنسخ-السابقة)
6. [هيكل البرانشات](#6-هيكل-البرانشات)
7. [ما يجب اختباره بعد التطبيق](#7-ما-يجب-اختباره-بعد-التطبيق)

---

## 1. ملخص الفحص الأمني

تم إجراء فحص أمني شامل لكامل الكود المصدري للمشروع وتم اكتشاف **15 ثغرة أمنية**:

| الخطورة | العدد | أمثلة |
|---------|-------|-------|
| **CRITICAL** | 3 | كلمات مرور بنص عادي، أي كلمة مرور تمرر في وضع التطوير، بيانات مكشوفة في GitHub |
| **HIGH** | 4 | لا يوجد Security Headers، Cookie غير مؤمن، لا يوجد Rate Limiting، GET Logout |
| **MEDIUM** | 5 | تسريب معلومات الأخطاء، XSS عبر innerHTML، عدم فحص محتوى الملفات، ثغرة في ExternalSync |
| **LOW** | 3 | Razor Runtime Compilation، لا يوجد تعقيد لكلمات المرور، لا يوجد Audit Logging |

**التقييم:** من **4.3/10** إلى **~7.5/10**

---

## 2. المراحل المنفذة

### المرحلة 1 — Security Headers وتأمين Cookie
- إضافة `X-Frame-Options: DENY` (حماية من clickjacking)
- إضافة `X-Content-Type-Options: nosniff`
- إضافة `Content-Security-Policy`
- إضافة `Referrer-Policy` و `Permissions-Policy`
- تأمين Cookie بـ `SecurePolicy=Always` و `SameSite=Lax`
- تقييد `Razor RuntimeCompilation` لبيئة التطوير فقط

### المرحلة 2+3 — تأمين المصادقة وتشفير كلمات المرور
- إصلاح Dev Fallback: كان يقبل أي كلمة مرور غير فارغة → الآن يتحقق من كلمة المرور المخزنة
- تطبيق `PasswordHasher<User>` لتشفير كلمات المرور
- التحويل التلقائي: عند أول تسجيل دخول بكلمة مرور قديمة (نص عادي) يتم تحويلها لـ hash تلقائياً
- إضافة `try-catch (FormatException)` لمعالجة كلمات المرور القديمة بدون أعطال
- حذف `LogoutGet()` (GET Logout بدون حماية CSRF)

### المرحلة 4 — إصلاح Authorization و Error Handling
- إضافة `IsAdminUser()` لصفحة `ExternalSync` (كانت متاحة لجميع المستخدمين)
- إخفاء `ex.Message` من المستخدمين في `HomeController` و `OrganizationApiController`
- إضافة `ILogger` للتسجيل الداخلي للأخطاء

### المرحلة 5 — إصلاح XSS
- إنشاء ملف `safe-dom.js` مع دوال آمنة (`safePopulateSelect`, `safeCreateElement`, `safeSetText`)
- تحميل الملف في `_Layout.cshtml` ليكون متاحاً لكل الصفحات
- استبدال `innerHTML` بدوال آمنة في `ExternalSync.cshtml`

### المرحلة 6 — Rate Limiting وتأمين الملفات
- إضافة Rate Limiting المدمج في .NET 8:
  - `login`: 10 محاولات كل 5 دقائق
  - `api`: 60 طلب بالدقيقة
- إضافة فحص Magic Bytes لملفات الصور (JPEG/PNG/GIF)
- تحديث `.gitignore` لحماية ملفات الأسرار والتحميلات

---

## 3. الملفات المعدلة

| الملف | المسار | المراحل |
|-------|--------|---------|
| `Program.cs` | `OperationalPlanMS/` | 1, 6 |
| `AccountController.cs` | `OperationalPlanMS/Controllers/` | 2, 3, 6 |
| `ProfileController.cs` | `OperationalPlanMS/Controllers/` | 3, 6 |
| `AdminController.cs` | `OperationalPlanMS/Controllers/` | 4 |
| `HomeController.cs` | `OperationalPlanMS/Controllers/` | 4 |
| `OrganizationApiController.cs` | `OperationalPlanMS/Controllers/` | 4, 6 |
| `ExternalSync.cshtml` | `OperationalPlanMS/Views/Admin/` | 5 |
| `_Layout.cshtml` | `OperationalPlanMS/Views/Shared/` | 5 |
| `safe-dom.js` | `OperationalPlanMS/wwwroot/js/` | 5 (ملف جديد) |
| `.gitignore` | جذر الـ Repository | 6 |

---

## 4. نقاط المرجعية (Backup Tags)

تم إنشاء 3 نقاط مرجعية (Tags) محفوظة على GitHub للرجوع إليها في أي وقت:

| النقطة | الـ Tag | الـ Commit | الوصف |
|--------|---------|-----------|-------|
| المشروع الأصلي | `backup/master-original` | `d022520` | master قبل أي دمج — المشروع الأساسي فقط |
| قبل الإصلاحات الأمنية | `backup/before-security-fixes` | `65f04d7` | يحتوي الوحدات التنظيمية + الصلاحيات بدون الإصلاحات الأمنية |
| بعد الإصلاحات الأمنية | `backup/after-security-fixes` | `22dc16c` | النسخة النهائية الكاملة مع كل الإصلاحات |

---

## 5. أوامر الرجوع للنسخ السابقة

### الرجوع لما قبل الإصلاحات الأمنية
لو الإصلاحات الأمنية سببت مشكلة وتبي ترجع للنسخة اللي فيها الصلاحيات بدون الأمان:
```powershell
cd C:\Users\MSI-Laptop\source\repos\OperationalPlanMS
git checkout master
git reset --hard backup/before-security-fixes
git push origin master --force
```

### الرجوع للمشروع الأصلي بالكامل
لو تبي ترجع master لحالته الأولى:
```powershell
cd C:\Users\MSI-Laptop\source\repos\OperationalPlanMS
git checkout master
git reset --hard backup/master-original
git push origin master --force
```

### إعادة النسخة النهائية (مع الإصلاحات الأمنية)
لو رجعت لنسخة سابقة وتبي تعيد النسخة الكاملة:
```powershell
cd C:\Users\MSI-Laptop\source\repos\OperationalPlanMS
git checkout master
git reset --hard backup/after-security-fixes
git push origin master --force
```

### ملاحظات مهمة
- الـ Tags **لا تتحذف** مع `git reset` — تبقى دائماً كنقاط رجوع
- البرانشات `feature/role-based-permissions` و `feature/security-fixes` باقية على GitHub كنسخ احتياطية
- `git reflog` يحفظ تاريخ 90 يوم حتى لو حذفت كل شيء

---

## 6. هيكل البرانشات

```
master (d022520) — المشروع الأساسي
  │
  └── feature/unify-organizational-units (5 commits)
        │   إعادة هيكلة الوحدات التنظيمية
        │   ✅ مدمج بالكامل في role-based-permissions
        │
        └── feature/role-based-permissions (4 commits إضافية)
              │   نظام الصلاحيات + Dashboard + Reports
              │   🏷️ backup/before-security-fixes
              │
              └── feature/security-fixes (1 commit)
                    │   الإصلاحات الأمنية (Phase 1-6)
                    │   🏷️ backup/after-security-fixes
                    │
                    └── ✅ مدمج في master (Fast-forward)
```

---

## 7. ما يجب اختباره بعد التطبيق

### تسجيل الدخول
- [ ] تسجيل دخول بكلمة مرور صحيحة — يعمل
- [ ] تسجيل دخول بكلمة مرور خاطئة — يرفض
- [ ] بعد أول دخول ناجح، تحقق من عمود `PasswordHash` في DB — يجب أن يكون hash طويل يبدأ بـ `AQAAAAIAAYag...`
- [ ] تسجيل دخول مرة ثانية بنفس كلمة المرور — يعمل (يتحقق من الـ hash)

### تغيير كلمة المرور
- [ ] تغيير كلمة المرور من صفحة Profile — يعمل
- [ ] تسجيل دخول بالكلمة الجديدة — يعمل
- [ ] الكلمة الجديدة مخزنة كـ hash في DB

### الصلاحيات
- [ ] مستخدم عادي يحاول فتح `/Admin/ExternalSync` — يرجع 403
- [ ] Admin يفتح `/Admin/ExternalSync` — يعمل

### Rate Limiting
- [ ] محاولة تسجيل دخول أكثر من 10 مرات خلال 5 دقائق — يرجع خطأ 429

### رفع الصور
- [ ] رفع صورة JPG/PNG حقيقية — يعمل
- [ ] رفع ملف HTML مسمى `.jpg` — يرفض

### Security Headers
- [ ] افتح DevTools > Network > اضغط على أي طلب > تحقق من Response Headers:
  - `X-Frame-Options: DENY`
  - `X-Content-Type-Options: nosniff`
  - `Content-Security-Policy: ...`

### Logout
- [ ] زر تسجيل الخروج يعمل (POST)
- [ ] فتح `/Account/LogoutGet` مباشرة — يرجع 404
