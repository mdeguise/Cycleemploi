using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeEmploiSnapshot",
                table: "RequestEmployees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GestionnaireSnapshot",
                table: "RequestEmployees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeEmploiSnapshot",
                table: "RequestEmployees",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeEmploiSnapshot",
                table: "RequestEmployees");

            migrationBuilder.DropColumn(
                name: "GestionnaireSnapshot",
                table: "RequestEmployees");

            migrationBuilder.DropColumn(
                name: "TypeEmploiSnapshot",
                table: "RequestEmployees");
        }
    }
}
