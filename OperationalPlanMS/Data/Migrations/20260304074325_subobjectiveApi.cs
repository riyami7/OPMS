using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class subobjectiveApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "SubObjectives",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ExternalUnitId",
                table: "SubObjectives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUnitName",
                table: "SubObjectives",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubObjectives_ExternalUnitId",
                table: "SubObjectives",
                column: "ExternalUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "SubObjectives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubObjectives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "SubObjectives");

            migrationBuilder.DropIndex(
                name: "IX_SubObjectives_ExternalUnitId",
                table: "SubObjectives");

            migrationBuilder.DropColumn(
                name: "ExternalUnitId",
                table: "SubObjectives");

            migrationBuilder.DropColumn(
                name: "ExternalUnitName",
                table: "SubObjectives");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "SubObjectives",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
