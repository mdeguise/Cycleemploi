using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365AccessApprovalAccessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessType",
                table: "D365AccessApprovals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessType",
                table: "D365AccessApprovals");
        }
    }
}
