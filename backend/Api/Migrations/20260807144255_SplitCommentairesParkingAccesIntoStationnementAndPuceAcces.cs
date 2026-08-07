using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitCommentairesParkingAccesIntoStationnementAndPuceAcces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommentairesParkingAcces",
                table: "OffboardingDetails",
                newName: "CommentairesStationnement");

            migrationBuilder.AddColumn<string>(
                name: "CommentairesPuceAcces",
                table: "OffboardingDetails",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentairesPuceAcces",
                table: "OffboardingDetails");

            migrationBuilder.RenameColumn(
                name: "CommentairesStationnement",
                table: "OffboardingDetails",
                newName: "CommentairesParkingAcces");
        }
    }
}
