using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFiscalYearFromInitiative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Initiatives_FiscalYears_FiscalYearId",
                table: "Initiatives");

            migrationBuilder.AlterColumn<int>(
                name: "FiscalYearId",
                table: "Initiatives",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_FiscalYears_FiscalYearId",
                table: "Initiatives",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Initiatives_FiscalYears_FiscalYearId",
                table: "Initiatives");

            migrationBuilder.AlterColumn<int>(
                name: "FiscalYearId",
                table: "Initiatives",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Initiatives_FiscalYears_FiscalYearId",
                table: "Initiatives",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
