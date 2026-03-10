using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class initiativeAPI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "Initiatives",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ExternalUnitId",
                table: "Initiatives",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUnitName",
                table: "Initiatives",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorEmpNumber",
                table: "Initiatives",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorName",
                table: "Initiatives",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorRank",
                table: "Initiatives",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_ExternalUnitId",
                table: "Initiatives",
                column: "ExternalUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Initiatives",
                column: "ExternalUnitId",
                principalTable: "ExternalOrganizationalUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Initiatives_ExternalOrganizationalUnits_ExternalUnitId",
                table: "Initiatives");

            migrationBuilder.DropIndex(
                name: "IX_Initiatives_ExternalUnitId",
                table: "Initiatives");

            migrationBuilder.DropColumn(
                name: "ExternalUnitId",
                table: "Initiatives");

            migrationBuilder.DropColumn(
                name: "ExternalUnitName",
                table: "Initiatives");

            migrationBuilder.DropColumn(
                name: "SupervisorEmpNumber",
                table: "Initiatives");

            migrationBuilder.DropColumn(
                name: "SupervisorName",
                table: "Initiatives");

            migrationBuilder.DropColumn(
                name: "SupervisorRank",
                table: "Initiatives");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationalUnitId",
                table: "Initiatives",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
