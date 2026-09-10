using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class RedesignD365ApprovalRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_D365Approvers_Sam_PositionTitle",
                table: "D365Approvers");

            migrationBuilder.DropColumn(
                name: "PositionTitle",
                table: "D365Approvers");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalRole",
                table: "D365Approvers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsDynaway",
                table: "D365AccessApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "D365AccessApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByDisplayName",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByObjectId",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Stage1ApprovedAt",
                table: "D365AccessApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage1ApprovedByDisplayName",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage1ApprovedByObjectId",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            // The entire approver set is being replaced: the old Position_Title-scoped rows (44 of
            // them — mostly Pierre-Luc Carpentier scoped to ~40 individual titles, plus a handful of
            // global approvers) are meaningless under the new fixed-role routing. Wipe the table and
            // seed exactly the 5 people the new process names.
            migrationBuilder.Sql("DELETE FROM D365Approvers");
            migrationBuilder.Sql(
                "INSERT INTO D365Approvers (Sam, DisplayName, Email, ApprovalRole, CreatedAt, CreatedByDisplayName) VALUES " +
                "('pcarpentier', N'Pierre-Luc Carpentier (T)', 'PCarpentier@tremblant.ca', 'Dynaway', GETUTCDATE(), N'Migration RedesignD365ApprovalRouting'), " +
                "('mbureau', N'Meganne Bureau (T)', 'MBureau@tremblant.ca', 'Stage1', GETUTCDATE(), N'Migration RedesignD365ApprovalRouting'), " +
                "('ablanchard', N'Annick Blanchard (T)', 'ABlanchard@tremblant.ca', 'Stage1', GETUTCDATE(), N'Migration RedesignD365ApprovalRouting'), " +
                "('mbessette', N'Marie-Eve Bessette (T)', 'MBessette@tremblant.ca', 'Stage2', GETUTCDATE(), N'Migration RedesignD365ApprovalRouting'), " +
                "('jmontreuil-emond', N'Jinny Montreuil-Emond (T)', 'JMontreuil-Emond@tremblant.ca', 'Stage2', GETUTCDATE(), N'Migration RedesignD365ApprovalRouting')");

            migrationBuilder.CreateIndex(
                name: "IX_D365Approvers_Sam_ApprovalRole",
                table: "D365Approvers",
                columns: new[] { "Sam", "ApprovalRole" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_D365Approvers_Sam_ApprovalRole",
                table: "D365Approvers");

            migrationBuilder.DropColumn(
                name: "ApprovalRole",
                table: "D365Approvers");

            migrationBuilder.DropColumn(
                name: "NeedsDynaway",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "RejectedByDisplayName",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "RejectedByObjectId",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "Stage1ApprovedAt",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "Stage1ApprovedByDisplayName",
                table: "D365AccessApprovals");

            migrationBuilder.DropColumn(
                name: "Stage1ApprovedByObjectId",
                table: "D365AccessApprovals");

            migrationBuilder.AddColumn<string>(
                name: "PositionTitle",
                table: "D365Approvers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_D365Approvers_Sam_PositionTitle",
                table: "D365Approvers",
                columns: new[] { "Sam", "PositionTitle" },
                unique: true);
        }
    }
}
