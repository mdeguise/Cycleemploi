using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365ApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "D365JobCodeTemplateRoles");

            migrationBuilder.DropTable(
                name: "D365JobCodeTemplates");

            migrationBuilder.CreateTable(
                name: "D365AccessApprovals",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    RequestEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JobTitleEnglish = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LegalEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApprovalLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ApAccessDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdditionalLegalEntities = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LevyEmployee = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedByObjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365AccessApprovals", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_D365AccessApprovals_RequestEmployees_RequestEmployeeId",
                        column: x => x.RequestEmployeeId,
                        principalTable: "RequestEmployees",
                        principalColumn: "RequestEmployeeId");
                    table.ForeignKey(
                        name: "FK_D365AccessApprovals_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D365Approvers",
                columns: table => new
                {
                    D365ApproverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sam = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PositionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365Approvers", x => x.D365ApproverId);
                });

            migrationBuilder.CreateTable(
                name: "D365AccessApprovalRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365AccessApprovalRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_D365AccessApprovalRoles_D365AccessApprovals_RequestId",
                        column: x => x.RequestId,
                        principalTable: "D365AccessApprovals",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_D365AccessApprovalRoles_RequestId_Role",
                table: "D365AccessApprovalRoles",
                columns: new[] { "RequestId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_D365AccessApprovals_RequestEmployeeId",
                table: "D365AccessApprovals",
                column: "RequestEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_D365Approvers_Sam_PositionTitle",
                table: "D365Approvers",
                columns: new[] { "Sam", "PositionTitle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "D365AccessApprovalRoles");

            migrationBuilder.DropTable(
                name: "D365Approvers");

            migrationBuilder.DropTable(
                name: "D365AccessApprovals");

            migrationBuilder.CreateTable(
                name: "D365JobCodeTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdditionalLegalEntities = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApAccessDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovalLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepartmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JobCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JobTitleEnglish = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegalEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LevyEmployee = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365JobCodeTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "D365JobCodeTemplateRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    D365JobCodeTemplateId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365JobCodeTemplateRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_D365JobCodeTemplateRoles_D365JobCodeTemplates_D365JobCodeTemplateId",
                        column: x => x.D365JobCodeTemplateId,
                        principalTable: "D365JobCodeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_D365JobCodeTemplateRoles_D365JobCodeTemplateId_Role",
                table: "D365JobCodeTemplateRoles",
                columns: new[] { "D365JobCodeTemplateId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_D365JobCodeTemplates_JobCode",
                table: "D365JobCodeTemplates",
                column: "JobCode",
                unique: true);
        }
    }
}
