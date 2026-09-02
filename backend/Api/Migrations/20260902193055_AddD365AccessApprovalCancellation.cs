using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365AccessApprovalCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "D365AccessApprovals",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "D365AccessApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByDisplayName",
                table: "D365AccessApprovals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByObjectId",
                table: "D365AccessApprovals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "CancelledByDisplayName",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "CancelledByObjectId",
                table: "D365AccessApprovals");
        }
    }
}
