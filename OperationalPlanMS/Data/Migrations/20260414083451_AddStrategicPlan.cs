using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategicPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StrategicPlanId",
                table: "StrategicAxes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StrategicPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartYear = table.Column<int>(type: "int", nullable: false),
                    EndYear = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedById = table.Column<int>(type: "int", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicPlans_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StrategicPlans_Users_LastModifiedById",
                        column: x => x.LastModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicAxes_StrategicPlanId",
                table: "StrategicAxes",
                column: "StrategicPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_CreatedById",
                table: "StrategicPlans",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicPlans_LastModifiedById",
                table: "StrategicPlans",
                column: "LastModifiedById");

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicAxes_StrategicPlans_StrategicPlanId",
                table: "StrategicAxes",
                column: "StrategicPlanId",
                principalTable: "StrategicPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StrategicAxes_StrategicPlans_StrategicPlanId",
                table: "StrategicAxes");

            migrationBuilder.DropTable(
                name: "StrategicPlans");

            migrationBuilder.DropIndex(
                name: "IX_StrategicAxes_StrategicPlanId",
                table: "StrategicAxes");

            migrationBuilder.DropColumn(
                name: "StrategicPlanId",
                table: "StrategicAxes");
        }
    }
}
