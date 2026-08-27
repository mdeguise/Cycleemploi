using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class RequestTicketsAndRequesterEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequesterEmail",
                table: "Requests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestTickets",
                columns: table => new
                {
                    RequestTicketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    RequestEmployeeId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FirstAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTickets", x => x.RequestTicketId);
                    table.ForeignKey(
                        name: "FK_RequestTickets_RequestEmployees_RequestEmployeeId",
                        column: x => x.RequestEmployeeId,
                        principalTable: "RequestEmployees",
                        principalColumn: "RequestEmployeeId");
                    table.ForeignKey(
                        name: "FK_RequestTickets_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestTickets_RequestEmployeeId",
                table: "RequestTickets",
                column: "RequestEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTickets_RequestId_Kind_RequestEmployeeId",
                table: "RequestTickets",
                columns: new[] { "RequestId", "Kind", "RequestEmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestTickets");

            migrationBuilder.DropColumn(
                name: "RequesterEmail",
                table: "Requests");
        }
    }
}
