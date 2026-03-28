using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Models;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Data
{
    /// <summary>
    /// بذر البيانات التجريبية العربية للنظام
    /// يُستدعى عند بدء التشغيل إذا كانت قاعدة البيانات فارغة
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>
        /// إدراج مع تفعيل IDENTITY_INSERT — يجب فتح الاتصال يدوياً
        /// حتى يبقى SET IDENTITY_INSERT ساري المفعول على نفس الـ session
        /// </summary>
        private static async Task InsertWithIdentity<T>(AppDbContext db, string tableName, IEnumerable<T> entities) where T : class
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            await db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] ON");
            db.Set<T>().AddRange(entities);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] OFF");

            // مسح الـ tracker حتى لا يتعارض مع الإدراجات التالية
            db.ChangeTracker.Clear();
        }

        public static async Task SeedAsync(AppDbContext db)
        {
            // لا نبذر إذا كانت البيانات موجودة مسبقاً
            if (await db.Roles.AnyAsync()) return;

            // فتح الاتصال مرة واحدة للحفاظ على session state (مطلوب لـ IDENTITY_INSERT)
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            // ========== 1. الأدوار ==========
            var roles = new List<Role>
            {
                new() { Id = 1, Code = "admin",      NameAr = "مدير النظام",    NameEn = "System Admin" },
                new() { Id = 2, Code = "executive",   NameAr = "التنفيذي",       NameEn = "Executive" },
                new() { Id = 3, Code = "supervisor",  NameAr = "المشرف",         NameEn = "Supervisor" },
                new() { Id = 4, Code = "user",        NameAr = "مدير المشروع",   NameEn = "Project Manager" },
                new() { Id = 7, Code = "stepuser",    NameAr = "منفذ الخطوة",    NameEn = "Step User" },
            };
            await InsertWithIdentity(db, "Roles", roles);

            // ========== 2. الوحدات التنظيمية (ValueGeneratedNever — لا تحتاج IDENTITY_INSERT) ==========
            var units = new List<ExternalOrganizationalUnit>
            {
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000100"), ParentId = null, TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "HQ",    ArabicName = "القيادة العامة",          IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000101"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "OPS",   ArabicName = "قسم العمليات",            IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000102"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "IT",    ArabicName = "قسم تقنية المعلومات",     IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000103"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "HR",    ArabicName = "قسم الموارد البشرية",     IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000104"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "FIN",   ArabicName = "قسم الشؤون المالية",      IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000105"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "LOG",   ArabicName = "قسم الإمداد والتموين",    IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000106"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000100"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "TRN",   ArabicName = "قسم التدريب والتأهيل",    IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000110"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000102"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "DEV",   ArabicName = "شعبة التطوير البرمجي",    IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000111"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000102"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "NET",   ArabicName = "شعبة الشبكات والبنية التحتية", IsActive = true },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000112"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000102"), TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "SEC",   ArabicName = "شعبة الأمن السيبراني",    IsActive = true },
            };
            db.ExternalOrganizationalUnits.AddRange(units);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // ========== 3. المستخدمون ==========
            var passwordHash = "$2a$11$K8GpFYMz5RVXYBnOvC8Cce5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y";
            var users = new List<User>
            {
                new() { Id = 1,  ADUsername = "admin",       FullNameAr = "أحمد بن سعيد الريامي",      FullNameEn = "Ahmed Al Riyami",        Email = "ahmed@opms.om",     RoleId = 1, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000100"), EmployeeRank = "مدير أول",    EmployeePosition = "مدير النظام",       BranchName = "القيادة العامة",          IsActive = true, IsStepApprover = true },
                new() { Id = 2,  ADUsername = "executive1",  FullNameAr = "خالد بن محمد البلوشي",      FullNameEn = "Khalid Al Balushi",      Email = "khalid@opms.om",    RoleId = 2, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000100"), EmployeeRank = "مدير تنفيذي",    EmployeePosition = "المدير التنفيذي",   BranchName = "القيادة العامة",          IsActive = true, IsStepApprover = true },
                new() { Id = 3,  ADUsername = "supervisor1", FullNameAr = "سالم بن عبدالله الحارثي",   FullNameEn = "Salem Al Harthi",        Email = "salem@opms.om",     RoleId = 3, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), EmployeeRank = "مدير قسم",    EmployeePosition = "مشرف العمليات",     BranchName = "قسم العمليات",            IsActive = true, IsStepApprover = true },
                new() { Id = 4,  ADUsername = "supervisor2", FullNameAr = "يوسف بن حمد الكندي",       FullNameEn = "Yousuf Al Kindi",        Email = "yousuf@opms.om",    RoleId = 3, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), EmployeeRank = "مدير قسم",    EmployeePosition = "مشرف تقنية المعلومات", BranchName = "قسم تقنية المعلومات", IsActive = true, IsStepApprover = true },
                new() { Id = 5,  ADUsername = "pm1",         FullNameAr = "ناصر بن علي المعمري",       FullNameEn = "Nasser Al Mamari",       Email = "nasser@opms.om",    RoleId = 4, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), EmployeeRank = "رئيس فريق",    EmployeePosition = "مدير مشروع",        BranchName = "قسم العمليات",            IsActive = true },
                new() { Id = 6,  ADUsername = "pm2",         FullNameAr = "محمد بن سيف الهنائي",       FullNameEn = "Mohammed Al Hinaai",     Email = "mohammed@opms.om",  RoleId = 4, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), EmployeeRank = "رئيس فريق",    EmployeePosition = "مدير مشروع",        BranchName = "قسم تقنية المعلومات",     IsActive = true },
                new() { Id = 7,  ADUsername = "pm3",         FullNameAr = "فهد بن خميس الراشدي",       FullNameEn = "Fahad Al Rashdi",        Email = "fahad@opms.om",     RoleId = 4, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000103"), EmployeeRank = "رئيس فريق",    EmployeePosition = "مدير مشروع",        BranchName = "قسم الموارد البشرية",     IsActive = true },
                new() { Id = 8,  ADUsername = "step1",       FullNameAr = "حمود بن راشد السعدي",       FullNameEn = "Hamoud Al Saadi",        Email = "hamoud@opms.om",    RoleId = 7, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000110"), EmployeeRank = "أخصائي أول",    EmployeePosition = "مطور أنظمة",        BranchName = "شعبة التطوير البرمجي",    IsActive = true },
                new() { Id = 9,  ADUsername = "step2",       FullNameAr = "عبدالله بن سالم الشكيلي",   FullNameEn = "Abdullah Al Shkaili",    Email = "abdullah@opms.om",  RoleId = 7, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000111"), EmployeeRank = "أخصائي أول",    EmployeePosition = "مهندس شبكات",       BranchName = "شعبة الشبكات والبنية التحتية", IsActive = true },
                new() { Id = 10, ADUsername = "step3",       FullNameAr = "سعود بن هلال العامري",      FullNameEn = "Saud Al Amri",           Email = "saud@opms.om",      RoleId = 7, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000112"), EmployeeRank = "أخصائي", EmployeePosition = "محلل أمن سيبراني", BranchName = "شعبة الأمن السيبراني",    IsActive = true },
                new() { Id = 11, ADUsername = "step4",       FullNameAr = "مازن بن خلفان البوسعيدي",   FullNameEn = "Mazen Al Busaidi",       Email = "mazen@opms.om",     RoleId = 7, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000106"), EmployeeRank = "فني",   EmployeePosition = "مدرب",              BranchName = "قسم التدريب والتأهيل",    IsActive = true },
                new() { Id = 12, ADUsername = "step5",       FullNameAr = "هيثم بن ناصر الوهيبي",      FullNameEn = "Haitham Al Wahaibi",     Email = "haitham@opms.om",   RoleId = 7, PasswordHash = passwordHash, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000104"), EmployeeRank = "فني",   EmployeePosition = "محاسب",             BranchName = "قسم الشؤون المالية",      IsActive = true },
            };
            await InsertWithIdentity(db, "Users", users);

            // ========== 4. إعدادات النظام ==========
            var settings = new List<SystemSettings>
            {
                new()
                {
                    Id = 1,
                    VisionAr = "الريادة في التخطيط التشغيلي والتميز المؤسسي على مستوى القوات المسلحة",
                    VisionEn = "Leadership in operational planning and institutional excellence across the armed forces",
                    MissionAr = "تحقيق الكفاءة في إدارة الخطط التشغيلية من خلال منهجية علمية متكاملة تضمن تحقيق الأهداف الاستراتيجية",
                    MissionEn = "Achieving efficiency in operational plan management through integrated scientific methodology ensuring strategic objectives are met",
                    DescriptionAr = "نظام إدارة الخطط التشغيلية - الإصدار 2.0",
                    DescriptionEn = "Operational Plan Management System - Version 2.0",
                    LastModifiedById = 1
                }
            };
            await InsertWithIdentity(db, "SystemSettings", settings);

            // ========== 5. إعدادات الوحدات التنظيمية (auto Id) ==========
            var unitSettings = new List<OrganizationalUnitSettings>
            {
                new() { ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات",        VisionAr = "التميز في تخطيط وتنفيذ العمليات",                     MissionAr = "ضمان جاهزية العمليات وتنفيذها وفق أعلى المعايير",                  CreatedById = 1 },
                new() { ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), ExternalUnitName = "قسم تقنية المعلومات", VisionAr = "قيادة التحول الرقمي والابتكار التقني",                 MissionAr = "توفير بنية تقنية متطورة وحلول ذكية تدعم الجاهزية والتميز المؤسسي", CreatedById = 1 },
                new() { ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000103"), ExternalUnitName = "قسم الموارد البشرية", VisionAr = "بناء رأس مال بشري متميز ومؤهل",                       MissionAr = "استقطاب وتطوير الكفاءات البشرية وتحقيق بيئة عمل محفزة",            CreatedById = 1 },
                new() { ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000104"), ExternalUnitName = "قسم الشؤون المالية",  VisionAr = "التميز في الإدارة المالية والرقابة",                   MissionAr = "ضمان الكفاءة في إدارة الموارد المالية والشفافية في التقارير",       CreatedById = 1 },
            };
            db.OrganizationalUnitSettings.AddRange(unitSettings);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // ========== 6. القيم المؤسسية (auto Id) ==========
            var coreValues = new List<CoreValue>
            {
                new() { NameAr = "النزاهة",     NameEn = "Integrity",    MeaningAr = "الالتزام بأعلى معايير الشفافية والأمانة في جميع التعاملات",        Icon = "bi-shield-check",   OrderIndex = 1, CreatedById = 1 },
                new() { NameAr = "التميز",      NameEn = "Excellence",   MeaningAr = "السعي الدائم نحو أعلى مستويات الجودة والإتقان في الأداء",          Icon = "bi-star-fill",      OrderIndex = 2, CreatedById = 1 },
                new() { NameAr = "العمل الجماعي", NameEn = "Teamwork",   MeaningAr = "تعزيز روح التعاون والعمل المشترك لتحقيق الأهداف المنشودة",          Icon = "bi-people-fill",    OrderIndex = 3, CreatedById = 1 },
                new() { NameAr = "الابتكار",    NameEn = "Innovation",   MeaningAr = "تبني الأفكار الإبداعية والحلول المبتكرة لمواجهة التحديات",          Icon = "bi-lightbulb-fill", OrderIndex = 4, CreatedById = 1 },
                new() { NameAr = "المسؤولية",   NameEn = "Accountability", MeaningAr = "تحمل المسؤولية والالتزام بتحقيق النتائج المطلوبة في الوقت المحدد", Icon = "bi-clipboard-check", OrderIndex = 5, CreatedById = 1 },
            };
            db.CoreValues.AddRange(coreValues);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // ========== 7. السنوات المالية ==========
            var fiscalYears = new List<FiscalYear>
            {
                new() { Id = 1, Year = 2025, NameAr = "السنة المالية 2025",  NameEn = "Fiscal Year 2025",  StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 12, 31), IsCurrent = false, CreatedBy = 1 },
                new() { Id = 2, Year = 2026, NameAr = "السنة المالية 2026",  NameEn = "Fiscal Year 2026",  StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsCurrent = true,  CreatedBy = 1 },
                new() { Id = 3, Year = 2027, NameAr = "السنة المالية 2027",  NameEn = "Fiscal Year 2027",  StartDate = new DateTime(2027, 1, 1), EndDate = new DateTime(2027, 12, 31), IsCurrent = false, CreatedBy = 1 },
            };
            await InsertWithIdentity(db, "FiscalYears", fiscalYears);

            // ========== 8. المحاور الاستراتيجية ==========
            var axes = new List<StrategicAxis>
            {
                new() { Id = 1, Code = "SA-01", NameAr = "الجاهزية والتميز التشغيلي",           NameEn = "Readiness & Operational Excellence",   DescriptionAr = "تعزيز الجاهزية التشغيلية وتحقيق التميز في الأداء",               OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, Code = "SA-02", NameAr = "التحول الرقمي والابتكار",             NameEn = "Digital Transformation & Innovation",  DescriptionAr = "تبني التقنيات الحديثة وتطوير البنية الرقمية",                     OrderIndex = 2, CreatedById = 1 },
                new() { Id = 3, Code = "SA-03", NameAr = "تنمية رأس المال البشري",              NameEn = "Human Capital Development",            DescriptionAr = "بناء وتطوير القدرات البشرية وتعزيز الكفاءات",                    OrderIndex = 3, CreatedById = 1 },
                new() { Id = 4, Code = "SA-04", NameAr = "الحوكمة والاستدامة المالية",           NameEn = "Governance & Financial Sustainability", DescriptionAr = "تعزيز الحوكمة المؤسسية وضمان الاستدامة المالية",                 OrderIndex = 4, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "StrategicAxes", axes);

            // ========== 9. الأهداف الاستراتيجية ==========
            var strategicObjectives = new List<StrategicObjective>
            {
                new() { Id = 1, Code = "SO-01", NameAr = "رفع مستوى الجاهزية التشغيلية",                 NameEn = "Enhance Operational Readiness",          StrategicAxisId = 1, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, Code = "SO-02", NameAr = "تحسين كفاءة العمليات الميدانية",                NameEn = "Improve Field Operations Efficiency",    StrategicAxisId = 1, OrderIndex = 2, CreatedById = 1 },
                new() { Id = 3, Code = "SO-03", NameAr = "تطوير البنية التحتية الرقمية",                  NameEn = "Develop Digital Infrastructure",          StrategicAxisId = 2, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 4, Code = "SO-04", NameAr = "تعزيز الأمن السيبراني",                         NameEn = "Strengthen Cybersecurity",                StrategicAxisId = 2, OrderIndex = 2, CreatedById = 1 },
                new() { Id = 5, Code = "SO-05", NameAr = "تطوير برامج التدريب والتأهيل",                  NameEn = "Develop Training & Qualification Programs", StrategicAxisId = 3, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 6, Code = "SO-06", NameAr = "استقطاب الكفاءات والمحافظة عليها",              NameEn = "Attract & Retain Talent",                StrategicAxisId = 3, OrderIndex = 2, CreatedById = 1 },
                new() { Id = 7, Code = "SO-07", NameAr = "تحسين كفاءة إدارة الموارد المالية",             NameEn = "Improve Financial Resource Management",  StrategicAxisId = 4, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 8, Code = "SO-08", NameAr = "تعزيز الحوكمة وإدارة المخاطر",                  NameEn = "Strengthen Governance & Risk Management", StrategicAxisId = 4, OrderIndex = 2, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "StrategicObjectives", strategicObjectives);

            // ========== 10. الأهداف الرئيسية ==========
            var mainObjectives = new List<MainObjective>
            {
                new() { Id = 1, Code = "MO-01", NameAr = "تحديث منظومة القيادة والسيطرة",                StrategicObjectiveId = 1, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, Code = "MO-02", NameAr = "تطوير إجراءات الاستجابة السريعة",              StrategicObjectiveId = 2, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 3, Code = "MO-03", NameAr = "بناء مركز بيانات متكامل",                       StrategicObjectiveId = 3, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 4, Code = "MO-04", NameAr = "تطبيق نظام إدارة الخطط التشغيلية",              StrategicObjectiveId = 3, OrderIndex = 2, CreatedById = 1 },
                new() { Id = 5, Code = "MO-05", NameAr = "تفعيل مركز العمليات الأمنية (SOC)",              StrategicObjectiveId = 4, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 6, Code = "MO-06", NameAr = "إطلاق برنامج القيادة المتقدمة",                  StrategicObjectiveId = 5, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 7, Code = "MO-07", NameAr = "تطوير نظام إدارة الأداء الوظيفي",               StrategicObjectiveId = 6, OrderIndex = 1, CreatedById = 1 },
                new() { Id = 8, Code = "MO-08", NameAr = "تطبيق نظام التخطيط المالي الشامل",               StrategicObjectiveId = 7, OrderIndex = 1, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "MainObjectives", mainObjectives);

            // ========== 11. الأهداف الفرعية ==========
            var subObjectives = new List<SubObjective>
            {
                new() { Id = 1, Code = "SUB-01", NameAr = "ترقية أنظمة الاتصالات المشفرة",        MainObjectiveId = 1, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات",        OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, Code = "SUB-02", NameAr = "تأهيل فرق الاستجابة الأولى",          MainObjectiveId = 2, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات",        OrderIndex = 1, CreatedById = 1 },
                new() { Id = 3, Code = "SUB-03", NameAr = "توفير بنية سحابية خاصة",              MainObjectiveId = 3, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), ExternalUnitName = "قسم تقنية المعلومات", OrderIndex = 1, CreatedById = 1 },
                new() { Id = 4, Code = "SUB-04", NameAr = "إطلاق نظام OPMS بالكامل",             MainObjectiveId = 4, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), ExternalUnitName = "قسم تقنية المعلومات", OrderIndex = 1, CreatedById = 1 },
                new() { Id = 5, Code = "SUB-05", NameAr = "بناء فريق الأمن السيبراني",            MainObjectiveId = 5, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000112"), ExternalUnitName = "شعبة الأمن السيبراني", OrderIndex = 1, CreatedById = 1 },
                new() { Id = 6, Code = "SUB-06", NameAr = "تنفيذ 4 دورات قيادية سنوياً",          MainObjectiveId = 6, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000106"), ExternalUnitName = "قسم التدريب والتأهيل", OrderIndex = 1, CreatedById = 1 },
                new() { Id = 7, Code = "SUB-07", NameAr = "أتمتة تقييم الأداء",                    MainObjectiveId = 7, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000103"), ExternalUnitName = "قسم الموارد البشرية", OrderIndex = 1, CreatedById = 1 },
                new() { Id = 8, Code = "SUB-08", NameAr = "تطبيق نظام ERP المالي",                MainObjectiveId = 8, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000104"), ExternalUnitName = "قسم الشؤون المالية",  OrderIndex = 1, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "SubObjectives", subObjectives);

            // ========== 12. التكاليف المالية ==========
            var financialCosts = new List<FinancialCost>
            {
                new() { Id = 1, NameAr = "ميزانية التشغيل",       NameEn = "Operating Budget",       DescriptionAr = "التكاليف التشغيلية الجارية",       OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, NameAr = "ميزانية رأس المال",     NameEn = "Capital Budget",         DescriptionAr = "مشتريات الأصول والمعدات الكبرى",    OrderIndex = 2, CreatedById = 1 },
                new() { Id = 3, NameAr = "ميزانية التدريب",       NameEn = "Training Budget",        DescriptionAr = "تكاليف البرامج التدريبية والتأهيلية", OrderIndex = 3, CreatedById = 1 },
                new() { Id = 4, NameAr = "ميزانية تقنية المعلومات", NameEn = "IT Budget",            DescriptionAr = "تكاليف البرمجيات والأجهزة والخدمات التقنية", OrderIndex = 4, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "FinancialCosts", financialCosts);

            // ========== 13. الجهات المساندة ==========
            var supportingEntities = new List<SupportingEntity>
            {
                new() { Id = 1, Code = "SE-01", NameAr = "وزارة الدفاع",            NameEn = "Ministry of Defence",       OrderIndex = 1, CreatedById = 1 },
                new() { Id = 2, Code = "SE-02", NameAr = "هيئة تقنية المعلومات",     NameEn = "IT Authority",              OrderIndex = 2, CreatedById = 1 },
                new() { Id = 3, Code = "SE-03", NameAr = "وزارة المالية",            NameEn = "Ministry of Finance",       OrderIndex = 3, CreatedById = 1 },
                new() { Id = 4, Code = "SE-04", NameAr = "المركز الوطني للأمن السيبراني", NameEn = "National Cybersecurity Center", OrderIndex = 4, CreatedById = 1 },
            };
            await InsertWithIdentity(db, "SupportingEntities", supportingEntities);

            // ========== 14. المبادرات ==========
            var now = DateTime.Now;
            var initiatives = new List<Initiative>
            {
                new() { Id = 1, Code = "INI-2026-001", NameAr = "تحديث البنية التحتية الرقمية", NameEn = "Digital Infrastructure Modernization", DescriptionAr = "مبادرة شاملة لتحديث وتطوير البنية التحتية التقنية بما يشمل الشبكات والخوادم والأنظمة السحابية", Status = Status.InProgress, Priority = Priority.Highest, PlannedStartDate = new DateTime(2026, 1, 1), PlannedEndDate = new DateTime(2026, 12, 31), ActualStartDate = new DateTime(2026, 1, 5), ProgressPercentage = 35, Weight = 30, Budget = 500000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), ExternalUnitName = "قسم تقنية المعلومات", SupervisorId = 4, SupervisorName = "يوسف بن حمد الكندي", SupervisorRank = "مدير قسم", FiscalYearId = 2, CreatedById = 1, CreatedAt = now.AddMonths(-3) },
                new() { Id = 2, Code = "INI-2026-002", NameAr = "تعزيز الجاهزية التشغيلية", NameEn = "Enhance Operational Readiness", DescriptionAr = "رفع مستوى جاهزية الوحدات الميدانية وتحديث خطط الطوارئ والاستجابة السريعة", Status = Status.InProgress, Priority = Priority.High, PlannedStartDate = new DateTime(2026, 2, 1), PlannedEndDate = new DateTime(2026, 11, 30), ActualStartDate = new DateTime(2026, 2, 3), ProgressPercentage = 20, Weight = 25, Budget = 350000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", SupervisorId = 3, SupervisorName = "سالم بن عبدالله الحارثي", SupervisorRank = "مدير قسم", FiscalYearId = 2, CreatedById = 1, CreatedAt = now.AddMonths(-2) },
                new() { Id = 3, Code = "INI-2026-003", NameAr = "برنامج تطوير القيادات", NameEn = "Leadership Development Program", DescriptionAr = "برنامج متكامل لتطوير المهارات القيادية والإدارية للكوادر الواعدة", Status = Status.InProgress, Priority = Priority.Medium, PlannedStartDate = new DateTime(2026, 3, 1), PlannedEndDate = new DateTime(2026, 12, 31), ActualStartDate = new DateTime(2026, 3, 2), ProgressPercentage = 15, Weight = 20, Budget = 200000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000106"), ExternalUnitName = "قسم التدريب والتأهيل", SupervisorId = 3, SupervisorName = "سالم بن عبدالله الحارثي", SupervisorRank = "مدير قسم", FiscalYearId = 2, CreatedById = 1, CreatedAt = now.AddMonths(-1) },
                new() { Id = 4, Code = "INI-2026-004", NameAr = "تحسين إدارة الموارد المالية", NameEn = "Financial Resource Management Improvement", DescriptionAr = "تطوير آليات التخطيط المالي والرقابة وتحسين كفاءة الإنفاق", Status = Status.Draft, Priority = Priority.Medium, PlannedStartDate = new DateTime(2026, 4, 1), PlannedEndDate = new DateTime(2026, 12, 31), ProgressPercentage = 0, Weight = 15, Budget = 150000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000104"), ExternalUnitName = "قسم الشؤون المالية", SupervisorId = 4, SupervisorName = "يوسف بن حمد الكندي", SupervisorRank = "مدير قسم", FiscalYearId = 2, CreatedById = 1, CreatedAt = now.AddDays(-15) },
                new() { Id = 5, Code = "INI-2025-001", NameAr = "تطوير نظام الاتصالات", NameEn = "Communications System Development", DescriptionAr = "ترقية نظام الاتصالات المشفرة وتوسيع التغطية الجغرافية", Status = Status.Completed, Priority = Priority.Highest, PlannedStartDate = new DateTime(2025, 1, 1), PlannedEndDate = new DateTime(2025, 12, 31), ActualStartDate = new DateTime(2025, 1, 3), ActualEndDate = new DateTime(2025, 11, 28), ProgressPercentage = 100, Weight = 10, Budget = 300000, ActualCost = 285000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", SupervisorId = 3, SupervisorName = "سالم بن عبدالله الحارثي", SupervisorRank = "مدير قسم", FiscalYearId = 1, CreatedById = 1, CreatedAt = now.AddMonths(-14) },
            };
            await InsertWithIdentity(db, "Initiatives", initiatives);

            // ========== 15. المشاريع ==========
            var projects = new List<Project>
            {
                new() { Id = 1, Code = "PRJ-2026-001", ProjectNumber = "P-001", NameAr = "تحديث شبكة الألياف الضوئية", NameEn = "Fiber Optic Network Upgrade", DescriptionAr = "استبدال شبكة النحاس القديمة بألياف ضوئية عالية السرعة وتوسيع التغطية", OperationalGoal = "رفع سرعة الشبكة إلى 10 جيجابت وتغطية 100% من المواقع", Status = Status.InProgress, Priority = Priority.Highest, Weight = 40, PlannedStartDate = new DateTime(2026, 1, 15), PlannedEndDate = new DateTime(2026, 9, 30), ActualStartDate = new DateTime(2026, 1, 20), ProgressPercentage = 45, Budget = 250000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000111"), ExternalUnitName = "شعبة الشبكات والبنية التحتية", ProjectManagerId = 6, ProjectManagerName = "محمد بن سيف الهنائي", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 3, FinancialCostId = 2, InitiativeId = 1, CreatedById = 1, CreatedAt = now.AddMonths(-3), ExpectedOutcomes = "شبكة ألياف ضوئية تغطي جميع المباني بسرعة 10 جيجابت", RiskNotes = "تأخر وصول المعدات من المورد" },
                new() { Id = 2, Code = "PRJ-2026-002", ProjectNumber = "P-002", NameAr = "نظام إدارة الخطط التشغيلية (OPMS)", NameEn = "Operational Plan Management System", DescriptionAr = "تطوير نظام ويب متكامل لإدارة الخطط التشغيلية والمبادرات والمشاريع", OperationalGoal = "أتمتة 100% من عمليات إدارة الخطط التشغيلية", Status = Status.InProgress, Priority = Priority.Highest, Weight = 35, PlannedStartDate = new DateTime(2026, 1, 1), PlannedEndDate = new DateTime(2026, 8, 31), ActualStartDate = new DateTime(2026, 1, 5), ProgressPercentage = 60, Budget = 150000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000110"), ExternalUnitName = "شعبة التطوير البرمجي", ProjectManagerId = 6, ProjectManagerName = "محمد بن سيف الهنائي", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 4, FinancialCostId = 4, InitiativeId = 1, CreatedById = 1, CreatedAt = now.AddMonths(-3), ExpectedOutcomes = "نظام متكامل يدعم العربية والإنجليزية مع لوحات تحكم تفاعلية", RiskNotes = "متطلبات جديدة قد تؤثر على الجدول الزمني" },
                new() { Id = 3, Code = "PRJ-2026-003", ProjectNumber = "P-003", NameAr = "تفعيل مركز البيانات السحابي", NameEn = "Cloud Data Center Activation", DescriptionAr = "إنشاء بنية سحابية خاصة لاستضافة الأنظمة والتطبيقات الداخلية", Status = Status.InProgress, Priority = Priority.High, Weight = 25, PlannedStartDate = new DateTime(2026, 3, 1), PlannedEndDate = new DateTime(2026, 12, 31), ActualStartDate = new DateTime(2026, 3, 5), ProgressPercentage = 15, Budget = 100000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000111"), ExternalUnitName = "شعبة الشبكات والبنية التحتية", ProjectManagerId = 6, ProjectManagerName = "محمد بن سيف الهنائي", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 3, FinancialCostId = 2, InitiativeId = 1, CreatedById = 1, CreatedAt = now.AddMonths(-1) },
                new() { Id = 4, Code = "PRJ-2026-004", ProjectNumber = "P-004", NameAr = "تحديث خطط الطوارئ", NameEn = "Emergency Plans Update", DescriptionAr = "مراجعة وتحديث جميع خطط الطوارئ والاستجابة السريعة وفق أحدث المعايير", Status = Status.InProgress, Priority = Priority.High, Weight = 50, PlannedStartDate = new DateTime(2026, 2, 1), PlannedEndDate = new DateTime(2026, 7, 31), ActualStartDate = new DateTime(2026, 2, 5), ProgressPercentage = 30, Budget = 80000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", ProjectManagerId = 5, ProjectManagerName = "ناصر بن علي المعمري", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 2, FinancialCostId = 1, InitiativeId = 2, CreatedById = 1, CreatedAt = now.AddMonths(-2) },
                new() { Id = 5, Code = "PRJ-2026-005", ProjectNumber = "P-005", NameAr = "تدريبات المحاكاة الميدانية", NameEn = "Field Simulation Exercises", DescriptionAr = "تنفيذ سلسلة من تدريبات المحاكاة لاختبار الجاهزية والاستجابة", Status = Status.InProgress, Priority = Priority.Medium, Weight = 50, PlannedStartDate = new DateTime(2026, 4, 1), PlannedEndDate = new DateTime(2026, 11, 30), ProgressPercentage = 10, Budget = 120000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", ProjectManagerId = 5, ProjectManagerName = "ناصر بن علي المعمري", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 2, FinancialCostId = 1, InitiativeId = 2, CreatedById = 1, CreatedAt = now.AddMonths(-1) },
                new() { Id = 6, Code = "PRJ-2026-006", ProjectNumber = "P-006", NameAr = "الدورة القيادية المتقدمة - الدفعة الأولى", NameEn = "Advanced Leadership Course - Batch 1", DescriptionAr = "دورة تدريبية مكثفة في القيادة الاستراتيجية والإدارة الحديثة", Status = Status.InProgress, Priority = Priority.Medium, Weight = 50, PlannedStartDate = new DateTime(2026, 3, 15), PlannedEndDate = new DateTime(2026, 6, 15), ActualStartDate = new DateTime(2026, 3, 15), ProgressPercentage = 25, Budget = 50000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000106"), ExternalUnitName = "قسم التدريب والتأهيل", ProjectManagerId = 7, ProjectManagerName = "فهد بن خميس الراشدي", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 6, FinancialCostId = 3, InitiativeId = 3, CreatedById = 1, CreatedAt = now.AddMonths(-1) },
                new() { Id = 7, Code = "PRJ-2025-001", ProjectNumber = "P-100", NameAr = "ترقية نظام الاتصالات المشفرة", NameEn = "Encrypted Communications Upgrade", DescriptionAr = "ترقية نظام الاتصالات المشفرة إلى الجيل الجديد", Status = Status.Completed, Priority = Priority.Highest, Weight = 100, PlannedStartDate = new DateTime(2025, 2, 1), PlannedEndDate = new DateTime(2025, 10, 31), ActualStartDate = new DateTime(2025, 2, 3), ActualEndDate = new DateTime(2025, 10, 15), ProgressPercentage = 100, Budget = 300000, ActualCost = 285000, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", ProjectManagerId = 5, ProjectManagerName = "ناصر بن علي المعمري", ProjectManagerRank = "رئيس فريق", SubObjectiveId = 1, FinancialCostId = 2, InitiativeId = 5, CreatedById = 1, CreatedAt = now.AddMonths(-14) },
            };
            await InsertWithIdentity(db, "Projects", projects);

            // ========== 16. الخطوات ==========
            var steps = new List<Step>
            {
                new() { Id = 1,  StepNumber = 1, NameAr = "تحليل المتطلبات",               NameEn = "Requirements Analysis",        ProjectId = 2, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 15, PlannedStartDate = new DateTime(2026,1,5),  PlannedEndDate = new DateTime(2026,1,31),  ActualStartDate = new DateTime(2026,1,5),  ActualEndDate = new DateTime(2026,1,28),  AssignedToId = 8,  AssignedToName = "حمود بن راشد السعدي", AssignedToRank = "أخصائي أول", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved, ApprovedById = 4, ApprovedAt = new DateTime(2026,1,29) },
                new() { Id = 2,  StepNumber = 2, NameAr = "تصميم قاعدة البيانات",           NameEn = "Database Design",              ProjectId = 2, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 10, PlannedStartDate = new DateTime(2026,2,1),  PlannedEndDate = new DateTime(2026,2,15),  ActualStartDate = new DateTime(2026,2,1),  ActualEndDate = new DateTime(2026,2,14),  AssignedToId = 8,  AssignedToName = "حمود بن راشد السعدي", AssignedToRank = "أخصائي أول", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved, ApprovedById = 4, ApprovedAt = new DateTime(2026,2,15) },
                new() { Id = 3,  StepNumber = 3, NameAr = "تطوير واجهة المستخدم",           NameEn = "Frontend Development",         ProjectId = 2, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 20, PlannedStartDate = new DateTime(2026,2,15), PlannedEndDate = new DateTime(2026,3,31),  ActualStartDate = new DateTime(2026,2,16), ActualEndDate = new DateTime(2026,3,28),  AssignedToId = 8,  AssignedToName = "حمود بن راشد السعدي", AssignedToRank = "أخصائي أول", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved, ApprovedById = 4, ApprovedAt = new DateTime(2026,3,29) },
                new() { Id = 4,  StepNumber = 4, NameAr = "تطوير الخدمات الخلفية",          NameEn = "Backend Services",             ProjectId = 2, Status = StepStatus.InProgress, ProgressPercentage = 75,  Weight = 20, PlannedStartDate = new DateTime(2026,3,1),  PlannedEndDate = new DateTime(2026,4,30),  ActualStartDate = new DateTime(2026,3,1),  AssignedToId = 8,  AssignedToName = "حمود بن راشد السعدي", AssignedToRank = "أخصائي أول", CreatedById = 1, DependsOnStepId = 2 },
                new() { Id = 5,  StepNumber = 5, NameAr = "اختبارات الأمان",                NameEn = "Security Testing",             ProjectId = 2, Status = StepStatus.InProgress, ProgressPercentage = 40,  Weight = 10, PlannedStartDate = new DateTime(2026,4,1),  PlannedEndDate = new DateTime(2026,5,15),  ActualStartDate = new DateTime(2026,4,1),  AssignedToId = 10, AssignedToName = "سعود بن هلال العامري", AssignedToRank = "أخصائي", CreatedById = 1 },
                new() { Id = 6,  StepNumber = 6, NameAr = "اختبار القبول (UAT)",            NameEn = "User Acceptance Testing",      ProjectId = 2, Status = StepStatus.NotStarted, ProgressPercentage = 0,   Weight = 10, PlannedStartDate = new DateTime(2026,5,15), PlannedEndDate = new DateTime(2026,6,30),  AssignedToId = 5,  AssignedToName = "ناصر بن علي المعمري", AssignedToRank = "رئيس فريق", CreatedById = 1, DependsOnStepId = 4 },
                new() { Id = 7,  StepNumber = 7, NameAr = "التدريب والنشر",                 NameEn = "Training & Deployment",        ProjectId = 2, Status = StepStatus.NotStarted, ProgressPercentage = 0,   Weight = 15, PlannedStartDate = new DateTime(2026,7,1),  PlannedEndDate = new DateTime(2026,8,31),  AssignedToId = 11, AssignedToName = "مازن بن خلفان البوسعيدي", AssignedToRank = "فني", CreatedById = 1, DependsOnStepId = 6 },
                new() { Id = 8,  StepNumber = 1, NameAr = "المسح الميداني وتحديد المسارات",  NameEn = "Site Survey & Route Planning", ProjectId = 1, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 20, PlannedStartDate = new DateTime(2026,1,15), PlannedEndDate = new DateTime(2026,2,28),  ActualStartDate = new DateTime(2026,1,20), ActualEndDate = new DateTime(2026,2,25),  AssignedToId = 9,  AssignedToName = "عبدالله بن سالم الشكيلي", AssignedToRank = "أخصائي أول", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved },
                new() { Id = 9,  StepNumber = 2, NameAr = "شراء المعدات والكابلات",          NameEn = "Equipment Procurement",        ProjectId = 1, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 25, PlannedStartDate = new DateTime(2026,2,1),  PlannedEndDate = new DateTime(2026,3,31),  ActualStartDate = new DateTime(2026,2,5),  ActualEndDate = new DateTime(2026,3,20),  AssignedToId = 12, AssignedToName = "هيثم بن ناصر الوهيبي", AssignedToRank = "فني", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved },
                new() { Id = 10, StepNumber = 3, NameAr = "تمديد الألياف الضوئية",           NameEn = "Fiber Installation",           ProjectId = 1, Status = StepStatus.InProgress, ProgressPercentage = 60,  Weight = 30, PlannedStartDate = new DateTime(2026,3,15), PlannedEndDate = new DateTime(2026,7,31),  ActualStartDate = new DateTime(2026,3,18), AssignedToId = 9,  AssignedToName = "عبدالله بن سالم الشكيلي", AssignedToRank = "أخصائي أول", CreatedById = 1, DependsOnStepId = 9 },
                new() { Id = 11, StepNumber = 4, NameAr = "الفحص والتشغيل التجريبي",         NameEn = "Testing & Commissioning",      ProjectId = 1, Status = StepStatus.NotStarted, ProgressPercentage = 0,   Weight = 25, PlannedStartDate = new DateTime(2026,8,1),  PlannedEndDate = new DateTime(2026,9,30),  AssignedToId = 9,  AssignedToName = "عبدالله بن سالم الشكيلي", AssignedToRank = "أخصائي أول", CreatedById = 1, DependsOnStepId = 10 },
                new() { Id = 12, StepNumber = 1, NameAr = "مراجعة الخطط الحالية",            NameEn = "Review Current Plans",          ProjectId = 4, Status = StepStatus.Completed,  ProgressPercentage = 100, Weight = 30, PlannedStartDate = new DateTime(2026,2,5),  PlannedEndDate = new DateTime(2026,3,15),  ActualStartDate = new DateTime(2026,2,5),  ActualEndDate = new DateTime(2026,3,10), AssignedToId = 5,  AssignedToName = "ناصر بن علي المعمري", AssignedToRank = "رئيس فريق", CreatedById = 1, ApprovalStatus = ApprovalStatus.Approved },
                new() { Id = 13, StepNumber = 2, NameAr = "إعداد الخطط المحدثة",             NameEn = "Prepare Updated Plans",         ProjectId = 4, Status = StepStatus.InProgress, ProgressPercentage = 50,  Weight = 40, PlannedStartDate = new DateTime(2026,3,15), PlannedEndDate = new DateTime(2026,5,31),  ActualStartDate = new DateTime(2026,3,16), AssignedToId = 5,  AssignedToName = "ناصر بن علي المعمري", AssignedToRank = "رئيس فريق", CreatedById = 1 },
                new() { Id = 14, StepNumber = 3, NameAr = "المراجعة والاعتماد",              NameEn = "Review & Approval",             ProjectId = 4, Status = StepStatus.NotStarted, ProgressPercentage = 0,   Weight = 30, PlannedStartDate = new DateTime(2026,6,1),  PlannedEndDate = new DateTime(2026,7,31),  AssignedToId = 3,  AssignedToName = "سالم بن عبدالله الحارثي", AssignedToRank = "مدير قسم", CreatedById = 1, DependsOnStepId = 13 },
            };
            await InsertWithIdentity(db, "Steps", steps);

            // ========== 17-22: الجداول بدون Id صريح (auto-generated) ==========

            db.Milestones.AddRange(new List<Milestone>
            {
                new() { NameAr = "اكتمال المسح الميداني",          NameEn = "Site Survey Complete",        DueDate = new DateTime(2026,2,28),  IsCompleted = true,  CompletedDate = new DateTime(2026,2,25), MilestoneType = MilestoneType.Checkpoint, ProjectId = 1, CreatedById = 1 },
                new() { NameAr = "وصول المعدات",                   NameEn = "Equipment Arrival",           DueDate = new DateTime(2026,3,31),  IsCompleted = true,  CompletedDate = new DateTime(2026,3,20), MilestoneType = MilestoneType.Delivery,   ProjectId = 1, CreatedById = 1 },
                new() { NameAr = "اكتمال تمديد الألياف",            NameEn = "Fiber Installation Complete", DueDate = new DateTime(2026,7,31),  IsCompleted = false, MilestoneType = MilestoneType.Checkpoint, ProjectId = 1, CreatedById = 1 },
                new() { NameAr = "إطلاق النسخة الأولى من OPMS",    NameEn = "OPMS v1.0 Launch",            DueDate = new DateTime(2026,5,1),   IsCompleted = false, MilestoneType = MilestoneType.Delivery,   Deliverable = "نسخة الإنتاج الأولى جاهزة للنشر", ProjectId = 2, CreatedById = 1 },
                new() { NameAr = "اكتمال التدريب",                 NameEn = "Training Complete",           DueDate = new DateTime(2026,8,15),  IsCompleted = false, MilestoneType = MilestoneType.Review,     ProjectId = 2, CreatedById = 1 },
                new() { NameAr = "اعتماد خطط الطوارئ المحدثة",     NameEn = "Updated Plans Approved",      DueDate = new DateTime(2026,7,31),  IsCompleted = false, MilestoneType = MilestoneType.Approval,   ProjectId = 4, CreatedById = 1 },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            db.ProjectKPIs.AddRange(new List<ProjectKPI>
            {
                new() { ProjectId = 1, KPIText = "نسبة تغطية الألياف الضوئية للمواقع", TargetValue = "100%", ActualValue = "45%", OrderIndex = 1 },
                new() { ProjectId = 1, KPIText = "سرعة الشبكة (جيجابت/ثانية)", TargetValue = "10", ActualValue = "10", OrderIndex = 2 },
                new() { ProjectId = 2, KPIText = "عدد الوحدات المفعّلة على النظام", TargetValue = "10", ActualValue = "3", OrderIndex = 1 },
                new() { ProjectId = 2, KPIText = "نسبة رضا المستخدمين", TargetValue = "90%", ActualValue = null, OrderIndex = 2 },
                new() { ProjectId = 2, KPIText = "عدد التقارير المؤتمتة", TargetValue = "15", ActualValue = "8", OrderIndex = 3 },
                new() { ProjectId = 4, KPIText = "عدد خطط الطوارئ المحدثة", TargetValue = "12", ActualValue = "4", OrderIndex = 1 },
                new() { ProjectId = 6, KPIText = "عدد المتدربين المجتازين", TargetValue = "25", ActualValue = "0", OrderIndex = 1 },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            db.ProjectRequirements.AddRange(new List<ProjectRequirement>
            {
                new() { ProjectId = 1, RequirementText = "توفير كابلات ألياف ضوئية من نوع Single-Mode", OrderIndex = 1 },
                new() { ProjectId = 1, RequirementText = "تخصيص فريق فني للتركيب والتمديد", OrderIndex = 2 },
                new() { ProjectId = 1, RequirementText = "تصاريح الحفر والتمديد من الجهات المعنية", OrderIndex = 3 },
                new() { ProjectId = 2, RequirementText = "خادم ويب بنظام Windows Server 2022", OrderIndex = 1 },
                new() { ProjectId = 2, RequirementText = "قاعدة بيانات SQL Server 2022", OrderIndex = 2 },
                new() { ProjectId = 2, RequirementText = "شهادة SSL للنطاق الداخلي", OrderIndex = 3 },
                new() { ProjectId = 2, RequirementText = "ربط مع Active Directory", OrderIndex = 4 },
                new() { ProjectId = 4, RequirementText = "نسخ من خطط الطوارئ الحالية", OrderIndex = 1 },
                new() { ProjectId = 4, RequirementText = "تقارير تحليل المخاطر الأخيرة", OrderIndex = 2 },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            db.ProjectSupportingUnits.AddRange(new List<ProjectSupportingUnit>
            {
                new() { ProjectId = 1, SupportingEntityId = 2, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000102"), ExternalUnitName = "قسم تقنية المعلومات", RepresentativeName = "يوسف بن حمد الكندي", RepresentativeRank = "مدير قسم" },
                new() { ProjectId = 1, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000105"), ExternalUnitName = "قسم الإمداد والتموين", RepresentativeName = "سعيد بن محمد الفارسي", RepresentativeRank = "رئيس فريق" },
                new() { ProjectId = 2, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000101"), ExternalUnitName = "قسم العمليات", RepresentativeName = "سالم بن عبدالله الحارثي", RepresentativeRank = "مدير قسم" },
                new() { ProjectId = 2, SupportingEntityId = 2, ExternalUnitId = Guid.Parse("10000000-0000-0000-0000-000000000112"), ExternalUnitName = "شعبة الأمن السيبراني", RepresentativeName = "سعود بن هلال العامري", RepresentativeRank = "أخصائي" },
                new() { ProjectId = 4, SupportingEntityId = 1, RepresentativeName = "علي بن سالم المطروشي", RepresentativeRank = "مدير أول" },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            db.ProjectYearTargets.AddRange(new List<ProjectYearTarget>
            {
                new() { ProjectId = 3, Year = 2026, TargetPercentage = 60, Notes = "إعداد البنية التحتية والمرحلة الأولى" },
                new() { ProjectId = 3, Year = 2027, TargetPercentage = 40, Notes = "إكمال النقل والتشغيل الكامل" },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            db.ProgressUpdates.AddRange(new List<ProgressUpdate>
            {
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 15, PreviousPercentage = 0, NotesAr = "اكتمال تحليل المتطلبات وتوثيقها", ProjectId = 2, StepId = 1, CreatedById = 8, CreatedAt = new DateTime(2026,1,28) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 25, PreviousPercentage = 15, NotesAr = "اكتمال تصميم قاعدة البيانات والعلاقات", ProjectId = 2, StepId = 2, CreatedById = 8, CreatedAt = new DateTime(2026,2,14) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 45, PreviousPercentage = 25, NotesAr = "اكتمال الواجهات الرئيسية ولوحة التحكم", ProjectId = 2, StepId = 3, CreatedById = 8, CreatedAt = new DateTime(2026,3,28) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 55, PreviousPercentage = 45, NotesAr = "تطوير Service Layer واختبارات الوحدة", ProjectId = 2, StepId = 4, CreatedById = 8, CreatedAt = new DateTime(2026,3,14) },
                new() { UpdateType = UpdateType.StatusChange, ProgressPercentage = 60, PreviousPercentage = 55, NotesAr = "إكمال Data API وتحسين أداء التقارير", Challenges = "تأخر بسيط في متطلبات التكامل مع الأنظمة الخارجية", NextSteps = "إكمال الخدمات الخلفية وبدء اختبارات الأمان", ProjectId = 2, CreatedById = 6, CreatedAt = now.AddDays(-3) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 20, PreviousPercentage = 0, NotesAr = "اكتمال المسح الميداني لجميع المواقع", ProjectId = 1, StepId = 8, CreatedById = 9, CreatedAt = new DateTime(2026,2,25) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 45, PreviousPercentage = 20, NotesAr = "استلام المعدات وبدء التمديد في المبنى الرئيسي", ProjectId = 1, StepId = 10, CreatedById = 9, CreatedAt = new DateTime(2026,3,20) },
                new() { UpdateType = UpdateType.Regular, ProgressPercentage = 35, PreviousPercentage = 20, NotesAr = "تقدم جيد في المشاريع الثلاثة", Challenges = "تأخر وصول بعض المعدات", NextSteps = "تسريع أعمال التمديد والتطوير", IsQuarterlyReport = true, Quarter = 1, PeriodStart = new DateTime(2026,1,1), PeriodEnd = new DateTime(2026,3,31), InitiativeId = 1, CreatedById = 4, CreatedAt = new DateTime(2026,4,1) },
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
    }
}