using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class visionandmisionApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId",
                table: "OrganizationalUnitSettings");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ExternalUnitId",
                table: "OrganizationalUnitSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUnitName",
                table: "OrganizationalUnitSettings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnitSettings_ExternalUnitId",
                table: "OrganizationalUnitSettings",
                column: "ExternalUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationalUnitSettings_ExternalOrganizationalUnits_ExternalUnitId",
                table: "OrganizationalUnitSettings",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationalUnitSettings_ExternalOrganizationalUnits_ExternalUnitId",
                table: "OrganizationalUnitSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId",
                table: "OrganizationalUnitSettings");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationalUnitSettings_ExternalUnitId",
                table: "OrganizationalUnitSettings");

            migrationBuilder.DropColumn(
                name: "ExternalUnitId",
                table: "OrganizationalUnitSettings");

            migrationBuilder.DropColumn(
                name: "ExternalUnitName",
                table: "OrganizationalUnitSettings");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationalUnitSettings_OrganizationalUnits_OrganizationalUnitId",
                table: "OrganizationalUnitSettings",
                column: "OrganizationalUnitId",
                principalTable: "OrganizationalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
