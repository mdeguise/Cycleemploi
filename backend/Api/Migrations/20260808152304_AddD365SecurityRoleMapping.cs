using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365SecurityRoleMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "D365SecurityRoleMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D365SecurityRoleMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_D365SecurityRoleMappings_JobCode_Role",
                table: "D365SecurityRoleMappings",
                columns: new[] { "JobCode", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "D365SecurityRoleMappings");
        }
    }
}
