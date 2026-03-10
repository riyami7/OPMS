using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrganizationAndOrganizationalUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Initiatives -> OrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Initiatives_OrganizationalUnits_OrganizationalUnitId') ALTER TABLE [Initiatives] DROP CONSTRAINT [FK_Initiatives_OrganizationalUnits_OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Initiativ__Organ__1CBC4616') ALTER TABLE [Initiatives] DROP CONSTRAINT [FK__Initiativ__Organ__1CBC4616];");

            // Initiatives -> ExternalOrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId') ALTER TABLE [Initiatives] DROP CONSTRAINT [FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId];");

            // OrganizationalUnitSettings -> OrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId') ALTER TABLE [OrganizationalUnitSettings] DROP CONSTRAINT [FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId];");

            // Projects -> OrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Projects_OrganizationalUnits_OrganizationalUnitId') ALTER TABLE [Projects] DROP CONSTRAINT [FK_Projects_OrganizationalUnits_OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Projects_OrganizationalUnits') ALTER TABLE [Projects] DROP CONSTRAINT [FK_Projects_OrganizationalUnits];");

            // SubObjectives -> OrganizationalUnits / ExternalOrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SubObjectives_OrganizationalUnits_OrganizationalUnitId') ALTER TABLE [SubObjectives] DROP CONSTRAINT [FK_SubObjectives_OrganizationalUnits_OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId') ALTER TABLE [SubObjectives] DROP CONSTRAINT [FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId];");

            // Users -> OrganizationalUnits
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_OrganizationalUnits_OrganizationalUnitId') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_OrganizationalUnits_OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_OrganizationalUnit') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_OrganizationalUnit];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_OrganizationalUnits_UnitId') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_OrganizationalUnits_UnitId];");

            // Users -> Organizations
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Organizations_OrganizationId') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Organizations_OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Organization') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Organization];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Organizations_OrganizationId1') ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Organizations_OrganizationId1];");

            // FiscalYears -> Organizations
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FiscalYears_Organizations_OrganizationId') ALTER TABLE [FiscalYears] DROP CONSTRAINT [FK_FiscalYears_Organizations_OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__FiscalYea__Organ__151B244E') ALTER TABLE [FiscalYears] DROP CONSTRAINT [FK__FiscalYea__Organ__151B244E];");

            // SupportingEntities -> Organizations
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SupportingEntities_Organizations_OrganizationId') ALTER TABLE [SupportingEntities] DROP CONSTRAINT [FK_SupportingEntities_Organizations_OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SupportingEntities_Organization') ALTER TABLE [SupportingEntities] DROP CONSTRAINT [FK_SupportingEntities_Organization];");

            // OrganizationalUnits self-reference و -> Organizations (يجب حذفها قبل حذف الجدول)
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrganizationalUnits_OrganizationalUnits_ParentUnitId') ALTER TABLE [OrganizationalUnits] DROP CONSTRAINT [FK_OrganizationalUnits_OrganizationalUnits_ParentUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrganizationalUnits_Organizations_OrganizationId') ALTER TABLE [OrganizationalUnits] DROP CONSTRAINT [FK_OrganizationalUnits_Organizations_OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrganizationalUnits_Organizations_OrganizationId1') ALTER TABLE [OrganizationalUnits] DROP CONSTRAINT [FK_OrganizationalUnits_Organizations_OrganizationId1];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[OrganizationalUnits]') AND type = 'U') DROP TABLE [OrganizationalUnits];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[Organizations]') AND type = 'U') DROP TABLE [Organizations];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_OrganizationalUnitId' AND object_id = OBJECT_ID('Users')) DROP INDEX [IX_Users_OrganizationalUnitId] ON [Users];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_OrganizationId' AND object_id = OBJECT_ID('Users')) DROP INDEX [IX_Users_OrganizationId] ON [Users];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupportingEntities_OrganizationId' AND object_id = OBJECT_ID('SupportingEntities')) DROP INDEX [IX_SupportingEntities_OrganizationId] ON [SupportingEntities];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SubObjectives_OrganizationalUnitId' AND object_id = OBJECT_ID('SubObjectives')) DROP INDEX [IX_SubObjectives_OrganizationalUnitId] ON [SubObjectives];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Projects_OrganizationalUnitId' AND object_id = OBJECT_ID('Projects')) DROP INDEX [IX_Projects_OrganizationalUnitId] ON [Projects];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrganizationalUnitSettings_OrganizationalUnitId' AND object_id = OBJECT_ID('OrganizationalUnitSettings')) DROP INDEX [IX_OrganizationalUnitSettings_OrganizationalUnitId] ON [OrganizationalUnitSettings];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Initiatives_OrganizationalUnitId' AND object_id = OBJECT_ID('Initiatives')) DROP INDEX [IX_Initiatives_OrganizationalUnitId] ON [Initiatives];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FiscalYears_OrganizationId' AND object_id = OBJECT_ID('FiscalYears')) DROP INDEX [IX_FiscalYears_OrganizationId] ON [FiscalYears];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationId' AND object_id = OBJECT_ID('Users')) ALTER TABLE [Users] DROP COLUMN [OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationalUnitId' AND object_id = OBJECT_ID('Users')) ALTER TABLE [Users] DROP COLUMN [OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationId' AND object_id = OBJECT_ID('SupportingEntities')) ALTER TABLE [SupportingEntities] DROP COLUMN [OrganizationId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationalUnitId' AND object_id = OBJECT_ID('SubObjectives')) ALTER TABLE [SubObjectives] DROP COLUMN [OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationalUnitId' AND object_id = OBJECT_ID('Projects')) ALTER TABLE [Projects] DROP COLUMN [OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationalUnitId' AND object_id = OBJECT_ID('OrganizationalUnitSettings')) ALTER TABLE [OrganizationalUnitSettings] DROP COLUMN [OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationalUnitId' AND object_id = OBJECT_ID('Initiatives')) ALTER TABLE [Initiatives] DROP COLUMN [OrganizationalUnitId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'OrganizationId' AND object_id = OBJECT_ID('FiscalYears')) ALTER TABLE [FiscalYears] DROP COLUMN [OrganizationId];");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_ExternalUnitId' AND object_id = OBJECT_ID('Users')) CREATE INDEX [IX_Users_ExternalUnitId] ON [Users] ([ExternalUnitId]);");

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Initiatives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "SubObjectives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Users",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Initiatives");

            migrationBuilder.DropForeignKey(
                name: "FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "SubObjectives");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalUnitId",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationalUnitId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "SupportingEntities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationalUnitId",
                table: "SubObjectives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationalUnitId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationalUnitId",
                table: "Initiatives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "FiscalYears",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationalUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    ParentUnitId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UnitLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationalUnits_OrganizationalUnits_ParentUnitId",
                        column: x => x.ParentUnitId,
                        principalTable: "OrganizationalUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationalUnits_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationalUnitId",
                table: "Users",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId",
                table: "Users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportingEntities_OrganizationId",
                table: "SupportingEntities",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubObjectives_OrganizationalUnitId",
                table: "SubObjectives",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationalUnitId",
                table: "Projects",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnitSettings_OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_OrganizationalUnitId",
                table: "Initiatives",
                column: "OrganizationalUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_OrganizationId",
                table: "FiscalYears",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_OrganizationId",
                table: "OrganizationalUnits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnits_ParentUnitId",
                table: "OrganizationalUnits",
                column: "ParentUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                table: "Organizations",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FiscalYears_Organizations_OrganizationId",
                table: "FiscalYears",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Initiatives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_OrganizationalUnits_OrganizationalUnitId",
                table: "Initiatives",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_OrganizationalUnits_OrganizationalUnitId",
                table: "Projects",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "SubObjectives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubObjectives_OrganizationalUnits_OrganizationalUnitId",
                table: "SubObjectives",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportingEntities_Organizations_OrganizationId",
                table: "SupportingEntities",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_OrganizationalUnits_OrganizationalUnitId",
                table: "Users",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}