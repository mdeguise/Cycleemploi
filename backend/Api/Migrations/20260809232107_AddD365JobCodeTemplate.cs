using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365JobCodeTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "D365JobCodeTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LegalEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovalLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApAccessDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdditionalLegalEntities = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "D365JobCodeTemplateRoles");

            migrationBuilder.DropTable(
                name: "D365JobCodeTemplates");
        }
    }
}
