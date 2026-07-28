using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TremblantLifecycle.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "RequestNumberSeq");

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByObjectId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "AccessDetails",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    BadgeZones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stationnement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_AccessDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationsDetails",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    AutreLogiciel = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationsDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_ApplicationsDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    AttachmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByObjectId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_Attachments_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentDetails",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_EquipmentDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OffboardingConfidentialComments",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    CommentaireRH = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByObjectId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OffboardingConfidentialComments", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_OffboardingConfidentialComments_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OffboardingDetails",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    DerniereJournee = table.Column<DateOnly>(type: "date", nullable: false),
                    IndemniteVacances = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RaisonArret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailsRaison = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reembaucheriez = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommentairesIT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentairesParkingAcces = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentairesRedingote = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OffboardingDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_OffboardingDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingDetails",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    DateEntreePrevue = table.Column<DateOnly>(type: "date", nullable: false),
                    RegleDePaye = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegleDePayeCommentaire = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_OnboardingDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestEmployees",
                columns: table => new
                {
                    RequestEmployeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    WorkdayEmployeeId = table.Column<int>(type: "int", nullable: false),
                    NameSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartementSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestEmployees", x => x.RequestEmployeeId);
                    table.ForeignKey(
                        name: "FK_RequestEmployees_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestAccessPos",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestAccessPos", x => new { x.RequestId, x.Value });
                    table.ForeignKey(
                        name: "FK_RequestAccessPos_AccessDetails_RequestId",
                        column: x => x.RequestId,
                        principalTable: "AccessDetails",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestAccessSystemes",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestAccessSystemes", x => new { x.RequestId, x.Value });
                    table.ForeignKey(
                        name: "FK_RequestAccessSystemes_AccessDetails_RequestId",
                        column: x => x.RequestId,
                        principalTable: "AccessDetails",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestApplications",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestApplications", x => new { x.RequestId, x.Value });
                    table.ForeignKey(
                        name: "FK_RequestApplications_ApplicationsDetails_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ApplicationsDetails",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestEquipments",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestEquipments", x => new { x.RequestId, x.Value });
                    table.ForeignKey(
                        name: "FK_RequestEquipments_EquipmentDetails_RequestId",
                        column: x => x.RequestId,
                        principalTable: "EquipmentDetails",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_RequestId",
                table: "Attachments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestEmployees_RequestId",
                table: "RequestEmployees",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestNumber",
                table: "Requests",
                column: "RequestNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "OffboardingConfidentialComments");

            migrationBuilder.DropTable(
                name: "OffboardingDetails");

            migrationBuilder.DropTable(
                name: "OnboardingDetails");

            migrationBuilder.DropTable(
                name: "RequestAccessPos");

            migrationBuilder.DropTable(
                name: "RequestAccessSystemes");

            migrationBuilder.DropTable(
                name: "RequestApplications");

            migrationBuilder.DropTable(
                name: "RequestEmployees");

            migrationBuilder.DropTable(
                name: "RequestEquipments");

            migrationBuilder.DropTable(
                name: "AccessDetails");

            migrationBuilder.DropTable(
                name: "ApplicationsDetails");

            migrationBuilder.DropTable(
                name: "EquipmentDetails");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropSequence(
                name: "RequestNumberSeq");
        }
    }
}
