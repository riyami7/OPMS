# خطوات تطبيق Migration

## 1. سحب الـ Branch

```bash
git fetch origin
git checkout feature/unify-organizational-units
```

## 2. فتح Package Manager Console في Visual Studio

`Tools` → `NuGet Package Manager` → `Package Manager Console`

تأكد أن الـ Default project هو: `OperationalPlanMS`

## 3. إنشاء الـ Migration

```powershell
Add-Migration RemoveOrganizationAndOrganizationalUnit
```

هذا الأمر سيُنشئ ملف Migration يحتوي على:
- حذف جداول: `Organizations`, `OrganizationalUnits`
- حذف أعمدة: `OrganizationalUnitId` من Initiatives, Projects, Users, SubObjectives, OrganizationalUnitSettings
- حذف أعمدة: `OrganizationId` من Users, FiscalYears, SupportingEntities

## 4. مراجعة ملف الـ Migration

افتح الملف المُنشأ في `Data/Migrations/` وتأكد من صحة العمليات قبل التطبيق.

## 5. تطبيق الـ Migration على قاعدة البيانات

```powershell
Update-Database
```

## 6. في حال الرجوع (Rollback)

```powershell
# الرجوع للـ Migration السابقة
Update-Database AddEmployeeApiFieldsToUsers1

# أو الرجوع لـ master
git checkout master
```
