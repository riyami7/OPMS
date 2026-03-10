using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel لعرض الخطة الاستراتيجية للزوار
    /// </summary>
    public class StrategicOverviewViewModel
    {
        /// <summary>
        /// إعدادات النظام (الرؤية والمهمة)
        /// </summary>
        public SystemSettings? SystemSettings { get; set; }

        /// <summary>
        /// إعدادات الوحدات التنظيمية (الرؤية والمهمة)
        /// </summary>
        public List<OrganizationalUnitSettings> UnitSettings { get; set; } = new();

        /// <summary>
        /// المحاور الرئيسية
        /// </summary>
        public List<StrategicAxis> Axes { get; set; } = new();

        /// <summary>
        /// الأهداف الاستراتيجية
        /// </summary>
        public List<StrategicObjective> StrategicObjectives { get; set; } = new();

        /// <summary>
        /// الأهداف الرئيسية
        /// </summary>
        public List<MainObjective> MainObjectives { get; set; } = new();

        /// <summary>
        /// الأهداف الفرعية
        /// </summary>
        public List<SubObjective> SubObjectives { get; set; } = new();

        /// <summary>
        /// القيم المؤسسية
        /// </summary>
        public List<CoreValue> CoreValues { get; set; } = new();
    }
}
