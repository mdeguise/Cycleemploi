using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingDepartmentComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentairesIT",
                table: "OnboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentairesPuceAcces",
                table: "OnboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentairesRedingote",
                table: "OnboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentairesStationnement",
                table: "OnboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OnboardingConfidentialComments",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    CommentaireRH = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByObjectId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingConfidentialComments", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_OnboardingConfidentialComments_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingConfidentialComments");

            migrationBuilder.DropColumn(
                name: "CommentairesIT",
                table: "OnboardingDetails");

            migrationBuilder.DropColumn(
                name: "CommentairesPuceAcces",
                table: "OnboardingDetails");

            migrationBuilder.DropColumn(
                name: "CommentairesRedingote",
                table: "OnboardingDetails");

            migrationBuilder.DropColumn(
                name: "CommentairesStationnement",
                table: "OnboardingDetails");
        }
    }
}
