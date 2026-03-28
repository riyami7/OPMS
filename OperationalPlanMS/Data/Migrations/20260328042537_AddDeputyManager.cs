using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeputyManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeputyManagerEmpNumber",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeputyManagerName",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeputyManagerRank",
                table: "Projects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeputyManagerEmpNumber",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeputyManagerName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeputyManagerRank",
                table: "Projects");
        }
    }
}
