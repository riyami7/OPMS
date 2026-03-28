using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiRepresentatives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportingUnitRepresentatives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectSupportingUnitId = table.Column<int>(type: "int", nullable: false),
                    EmpNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportingUnitRepresentatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportingUnitRepresentatives_ProjectSupportingUnits_ProjectSupportingUnitId",
                        column: x => x.ProjectSupportingUnitId,
                        principalTable: "ProjectSupportingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportingUnitRepresentatives_ProjectSupportingUnitId_EmpNumber",
                table: "SupportingUnitRepresentatives",
                columns: new[] { "ProjectSupportingUnitId", "EmpNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportingUnitRepresentatives");
        }
    }
}
