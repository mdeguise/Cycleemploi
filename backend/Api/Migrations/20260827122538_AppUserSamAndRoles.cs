using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <summary>
    /// Moves AppUsers off email-keyed authorization onto the bare sAMAccountName, and adds a Role.
    ///
    /// HAND-EDITED after scaffolding, deliberately — do not regenerate. The scaffolded version added
    /// Sam with defaultValue "" for every existing row and then created a UNIQUE index on it, which
    /// fails outright as soon as there is more than one row, and left Role as "" rather than a valid
    /// enum value. The order here is: add the columns nullable -> backfill -> tighten to NOT NULL ->
    /// create the unique index.
    ///
    /// Backfill assumption, verified against the live table before writing this: every existing row
    /// is a Tremblant address whose local part IS the sAMAccountName (MDeGuise@tremblant.ca ->
    /// mdeguise). If a row's email ever stops following that rule, the row must be corrected by hand
    /// in the Administrateurs screen — the unique index below will refuse duplicates rather than
    /// silently granting one person another person's access.
    /// </summary>
    public partial class AppUserSamAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers");

            // Email becomes informational and optional: admin (*_adm) accounts have no `mail`
            // attribute in AD, which is precisely why it can no longer be the authorization key.
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AppUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Sam",
                table: "AppUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfill Sam from the email local part, lowercased.
            migrationBuilder.Sql(@"
UPDATE dbo.AppUsers
SET Sam = LOWER(LTRIM(RTRIM(
        CASE WHEN CHARINDEX('@', Email) > 0
             THEN LEFT(Email, CHARINDEX('@', Email) - 1)
             ELSE Email
        END)))
WHERE (Sam IS NULL OR Sam = '') AND Email IS NOT NULL AND LTRIM(RTRIM(Email)) <> '';");

            // Every pre-existing row was a Ticket Template admin, so Admin is the faithful mapping —
            // Lecteur did not exist before this migration.
            migrationBuilder.Sql(@"
UPDATE dbo.AppUsers SET Role = 'Admin' WHERE Role IS NULL OR Role = '';");

            // Fail loudly rather than create an unusable row: a NULL/blank Sam here would mean an
            // existing row had no usable email, and silently dropping it would revoke someone's
            // access without telling anyone.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Sam IS NULL OR LTRIM(RTRIM(Sam)) = '')
    THROW 50000, 'AppUsers backfill failed: at least one row has no email to derive a sAMAccountName from. Fix those rows by hand, then re-run this migration.', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "Sam",
                table: "AppUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "AppUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Sam",
                table: "AppUsers",
                column: "Sam",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Sam",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "Sam",
                table: "AppUsers");

            // Reverting restores the unique index on Email, so any row added after this migration
            // with a NULL or duplicate Email has to be cleaned up first — rolling back is a manual
            // operation, not a safe automatic one.
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AppUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);
        }
    }
}
