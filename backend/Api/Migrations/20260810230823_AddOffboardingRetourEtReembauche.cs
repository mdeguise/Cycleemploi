using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOffboardingRetourEtReembauche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateRetourConnue",
                table: "OffboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateRetourTravail",
                table: "OffboardingDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifNonAdmissibilite",
                table: "OffboardingDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreavisRecu",
                table: "OffboardingDetails",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateRetourConnue",
                table: "OffboardingDetails");

            migrationBuilder.DropColumn(
                name: "DateRetourTravail",
                table: "OffboardingDetails");

            migrationBuilder.DropColumn(
                name: "MotifNonAdmissibilite",
                table: "OffboardingDetails");

            migrationBuilder.DropColumn(
                name: "PreavisRecu",
                table: "OffboardingDetails");
        }
    }
}
