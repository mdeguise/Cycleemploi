using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssueReports",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBySam = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Expected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Screen = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Context = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommitRef = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    WorkLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HandledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledBySam = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueReports", x => x.IssueId);
                });

            migrationBuilder.CreateTable(
                name: "IssueReportImages",
                columns: table => new
                {
                    ImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<byte>(type: "tinyint", nullable: false),
                    MimeType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueReportImages", x => x.ImageId);
                    table.ForeignKey(
                        name: "FK_IssueReportImages_IssueReports_IssueId",
                        column: x => x.IssueId,
                        principalTable: "IssueReports",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueReportImages_IssueId_Ordinal",
                table: "IssueReportImages",
                columns: new[] { "IssueId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_CreatedBySam_CreatedUtc",
                table: "IssueReports",
                columns: new[] { "CreatedBySam", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_Status_CreatedUtc",
                table: "IssueReports",
                columns: new[] { "Status", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueReportImages");

            migrationBuilder.DropTable(
                name: "IssueReports");
        }
    }
}
