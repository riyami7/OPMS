using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStepKPIsAndSupportingUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StepKPIs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepId = table.Column<int>(type: "int", nullable: false),
                    Indicator = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepKPIs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepKPIs_Steps_StepId",
                        column: x => x.StepId,
                        principalTable: "Steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StepSupportingUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepId = table.Column<int>(type: "int", nullable: false),
                    ProjectSupportingUnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepSupportingUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepSupportingUnits_ProjectSupportingUnits_ProjectSupportingUnitId",
                        column: x => x.ProjectSupportingUnitId,
                        principalTable: "ProjectSupportingUnits",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StepSupportingUnits_Steps_StepId",
                        column: x => x.StepId,
                        principalTable: "Steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepKPIs_StepId",
                table: "StepKPIs",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_StepSupportingUnits_ProjectSupportingUnitId",
                table: "StepSupportingUnits",
                column: "ProjectSupportingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StepSupportingUnits_StepId_ProjectSupportingUnitId",
                table: "StepSupportingUnits",
                columns: new[] { "StepId", "ProjectSupportingUnitId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepKPIs");

            migrationBuilder.DropTable(
                name: "StepSupportingUnits");
        }
    }
}
