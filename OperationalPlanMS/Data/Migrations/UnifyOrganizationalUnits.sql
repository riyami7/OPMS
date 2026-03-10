-- =====================================================
-- Migration: توحيد الوحدات التنظيمية
-- الهدف: حذف جداول Organization و OrganizationalUnit
--        والاعتماد الكلي على ExternalOrganizationalUnits
-- =====================================================

BEGIN TRANSACTION;

-- 1. حذف الأعمدة المرتبطة بـ OrganizationalUnit من جدول Initiatives
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Initiatives' AND COLUMN_NAME='OrganizationalUnitId')
BEGIN
    ALTER TABLE Initiatives DROP CONSTRAINT IF EXISTS FK_Initiatives_OrganizationalUnits_OrganizationalUnitId;
    ALTER TABLE Initiatives DROP COLUMN OrganizationalUnitId;
END

-- 2. حذف الأعمدة المرتبطة بـ OrganizationalUnit من جدول Projects
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Projects' AND COLUMN_NAME='OrganizationalUnitId')
BEGIN
    ALTER TABLE Projects DROP CONSTRAINT IF EXISTS FK_Projects_OrganizationalUnits_OrganizationalUnitId;
    ALTER TABLE Projects DROP COLUMN OrganizationalUnitId;
END

-- 3. حذف الأعمدة من جدول Users
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='OrganizationalUnitId')
BEGIN
    ALTER TABLE Users DROP CONSTRAINT IF EXISTS FK_Users_OrganizationalUnits_OrganizationalUnitId;
    ALTER TABLE Users DROP COLUMN OrganizationalUnitId;
END

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='OrganizationId')
BEGIN
    ALTER TABLE Users DROP CONSTRAINT IF EXISTS FK_Users_Organizations_OrganizationId;
    ALTER TABLE Users DROP COLUMN OrganizationId;
END

-- 4. حذف OrganizationalUnitId من SubObjectives
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SubObjectives' AND COLUMN_NAME='OrganizationalUnitId')
BEGIN
    ALTER TABLE SubObjectives DROP CONSTRAINT IF EXISTS FK_SubObjectives_OrganizationalUnits_OrganizationalUnitId;
    ALTER TABLE SubObjectives DROP COLUMN OrganizationalUnitId;
END

-- 5. حذف OrganizationId من FiscalYears
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FiscalYears' AND COLUMN_NAME='OrganizationId')
BEGIN
    ALTER TABLE FiscalYears DROP CONSTRAINT IF EXISTS FK_FiscalYears_Organizations_OrganizationId;
    ALTER TABLE FiscalYears DROP COLUMN OrganizationId;
END

-- 6. حذف OrganizationId من SupportingEntities
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SupportingEntities' AND COLUMN_NAME='OrganizationId')
BEGIN
    ALTER TABLE SupportingEntities DROP CONSTRAINT IF EXISTS FK_SupportingEntities_Organizations_OrganizationId;
    ALTER TABLE SupportingEntities DROP COLUMN OrganizationId;
END

-- 7. حذف OrganizationalUnitId من OrganizationalUnitSettings
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='OrganizationalUnitSettings' AND COLUMN_NAME='OrganizationalUnitId')
BEGIN
    ALTER TABLE OrganizationalUnitSettings DROP CONSTRAINT IF EXISTS FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId;
    ALTER TABLE OrganizationalUnitSettings DROP COLUMN OrganizationalUnitId;
END

-- 8. حذف جدول OrganizationalUnits (بعد حذف كل الـ FK)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='OrganizationalUnits')
BEGIN
    DROP TABLE OrganizationalUnits;
END

-- 9. حذف جدول Organizations
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Organizations')
BEGIN
    DROP TABLE Organizations;
END

COMMIT TRANSACTION;

PRINT 'Migration completed successfully!';
