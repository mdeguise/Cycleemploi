using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddD365ApprovalCategoryAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalCategory",
                table: "D365AccessApprovals",
                type: "nvarchar(max)",
                nullable: true);

            // New Dynaway/Procurement/Other routing roles, added ALONGSIDE the existing Dynaway/
            // Stage1/Stage2 rows (never deleted) — approvals created before this migration have
            // ApprovalCategory = null and keep routing through the old generic Stage1/Stage2 roles,
            // per explicit user decision. Same 4 people, new role assignments:
            // ablanchard/mbureau -> ProcurementStage1, mbessette/jmontreuil-emond -> ProcurementStage2
            // AND Other (they hold two roles each under the new model).
            migrationBuilder.Sql(
                "INSERT INTO D365Approvers (Sam, DisplayName, Email, ApprovalRole, CreatedAt, CreatedByDisplayName) VALUES " +
                "('ablanchard', N'Annick Blanchard (T)', 'ABlanchard@tremblant.ca', 'ProcurementStage1', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles'), " +
                "('mbureau', N'Meganne Bureau (T)', 'MBureau@tremblant.ca', 'ProcurementStage1', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles'), " +
                "('mbessette', N'Marie-Eve Bessette (T)', 'MBessette@tremblant.ca', 'ProcurementStage2', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles'), " +
                "('jmontreuil-emond', N'Jinny Montreuil-Emond (T)', 'JMontreuil-Emond@tremblant.ca', 'ProcurementStage2', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles'), " +
                "('mbessette', N'Marie-Eve Bessette (T)', 'MBessette@tremblant.ca', 'Other', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles'), " +
                "('jmontreuil-emond', N'Jinny Montreuil-Emond (T)', 'JMontreuil-Emond@tremblant.ca', 'Other', GETUTCDATE(), N'Migration AddD365ApprovalCategoryAndRoles')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalCategory",
                table: "D365AccessApprovals");
        }
    }
}
