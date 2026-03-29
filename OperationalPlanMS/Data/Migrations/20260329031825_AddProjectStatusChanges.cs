using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationalPlanMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStatusChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectStatusChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ObstacleType = table.Column<int>(type: "int", nullable: true),
                    ObstacleDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpectedResumeDate = table.Column<DateTime>(type: "date", nullable: true),
                    ChangedById = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusChanges_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectStatusChanges_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusChanges_ChangedById",
                table: "ProjectStatusChanges",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusChanges_ProjectId",
                table: "ProjectStatusChanges",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectStatusChanges");
        }
    }
}
