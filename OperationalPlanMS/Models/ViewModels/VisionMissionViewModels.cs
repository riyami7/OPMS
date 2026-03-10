using Microsoft.AspNetCore.Mvc.Rendering;
using OperationalPlanMS.Models.Entities;

namespace OperationalPlanMS.Models.ViewModels
{
    /// <summary>
    /// ViewModel للرؤية والمهمة
    /// </summary>
    public class VisionMissionViewModel
    {
        /// <summary>
        /// إعدادات النظام العامة
        /// </summary>
        public SystemSettingsViewModel SystemSettings { get; set; } = new();

        /// <summary>
        /// إعدادات الوحدات التنظيمية
        /// </summary>
        public List<OrganizationalUnitSettings> UnitSettings { get; set; } = new();

        /// <summary>
        /// الوحدات المتاحة للإضافة (التي ليس لها إعدادات بعد) - للتوافق القديم
        /// </summary>
        public SelectList? AvailableUnits { get; set; }
    }

    /// <summary>
    /// ViewModel لإعدادات النظام
    /// </summary>
    public class SystemSettingsViewModel
    {
        public int Id { get; set; }

        public string? VisionAr { get; set; }

        public string? VisionEn { get; set; }

        public string? MissionAr { get; set; }

        public string? MissionEn { get; set; }

        public string? DescriptionAr { get; set; }

        public string? DescriptionEn { get; set; }

        public DateTime? LastModifiedAt { get; set; }

        public User? LastModifiedBy { get; set; }

        public static SystemSettingsViewModel FromEntity(SystemSettings? entity)
        {
            if (entity == null)
                return new SystemSettingsViewModel();

            return new SystemSettingsViewModel
            {
                Id = entity.Id,
                VisionAr = entity.VisionAr,
                VisionEn = entity.VisionEn,
                MissionAr = entity.MissionAr,
                MissionEn = entity.MissionEn,
                DescriptionAr = entity.DescriptionAr,
                DescriptionEn = entity.DescriptionEn,
                LastModifiedAt = entity.LastModifiedAt,
                LastModifiedBy = entity.LastModifiedBy
            };
        }

        public void UpdateEntity(SystemSettings entity)
        {
            entity.VisionAr = VisionAr;
            entity.VisionEn = VisionEn;
            entity.MissionAr = MissionAr;
            entity.MissionEn = MissionEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;
        }
    }

    /// <summary>
    /// ViewModel لإعدادات الوحدة التنظيمية
    /// </summary>
    public class UnitSettingsFormViewModel
    {
        public int Id { get; set; }

        // ========== الحقول القديمة (للتوافق) ==========

        // ========== الحقول الجديدة من API ==========
        public int? ExternalUnitId { get; set; }
        public string? ExternalUnitName { get; set; }

        // ==========================================================

        public string? VisionAr { get; set; }

        public string? VisionEn { get; set; }

        public string? MissionAr { get; set; }

        public string? MissionEn { get; set; }

        public string? DescriptionAr { get; set; }

        public string? DescriptionEn { get; set; }

        // للعرض
        public string? UnitName { get; set; }


        public static UnitSettingsFormViewModel FromEntity(OrganizationalUnitSettings entity)
        {
            return new UnitSettingsFormViewModel
            {
                Id = entity.Id,
                ExternalUnitId = entity.ExternalUnitId,
                ExternalUnitName = entity.ExternalUnitName,
                VisionAr = entity.VisionAr,
                VisionEn = entity.VisionEn,
                MissionAr = entity.MissionAr,
                MissionEn = entity.MissionEn,
                DescriptionAr = entity.DescriptionAr,
                DescriptionEn = entity.DescriptionEn,
                UnitName = entity.UnitDisplayName
            };
        }

        public void UpdateEntity(OrganizationalUnitSettings entity)
        {
            entity.VisionAr = VisionAr;
            entity.VisionEn = VisionEn;
            entity.MissionAr = MissionAr;
            entity.MissionEn = MissionEn;
            entity.DescriptionAr = DescriptionAr;
            entity.DescriptionEn = DescriptionEn;

            // الحقول الجديدة من API
            entity.ExternalUnitId = ExternalUnitId;
            entity.ExternalUnitName = ExternalUnitName;

            // الحقل القديم - null إذا استخدمنا API
            if (ExternalUnitId.HasValue)
            {
            }
            else
            {
            }
        }
    }
}