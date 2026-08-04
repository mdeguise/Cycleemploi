using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBesoinCodeAlarmeWithDetailsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BesoinCodeAlarme",
                table: "AccessDetails");

            migrationBuilder.AddColumn<string>(
                name: "CodeAlarmeDetails",
                table: "AccessDetails",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeAlarmeDetails",
                table: "AccessDetails");

            migrationBuilder.AddColumn<bool>(
                name: "BesoinCodeAlarme",
                table: "AccessDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
