using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Bootstrap.API.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BootstrapSchoolRegistryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BootstrapSchoolRegistry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActivationBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CloudBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicKeyFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    KeyVersion = table.Column<int>(type: "int", nullable: true),
                    ServerInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BootstrapSchoolRegistry", x => x.Id);
                    table.UniqueConstraint("AK_BootstrapSchoolRegistry_SchoolId", x => x.SchoolId);
                });

            migrationBuilder.CreateTable(
                name: "BootstrapSchoolEstablishmentCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
                    TokenType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BootstrapSchoolEstablishmentCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BootstrapSchoolEstablishmentCredentials_BootstrapSchoolRegistry_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "BootstrapSchoolRegistry",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BootstrapEstablishmentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BootstrapEstablishmentSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BootstrapEstablishmentSessions_BootstrapSchoolEstablishmentCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalTable: "BootstrapSchoolEstablishmentCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BootstrapEstablishmentSessions_BootstrapSchoolRegistry_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "BootstrapSchoolRegistry",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BootstrapEstablishmentSessions_CredentialId",
                table: "BootstrapEstablishmentSessions",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_BootstrapEstablishmentSessions_SchoolId",
                table: "BootstrapEstablishmentSessions",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_Device_Status",
                table: "BootstrapEstablishmentSessions",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EstablishmentCredential_SchoolId_Version",
                table: "BootstrapSchoolEstablishmentCredentials",
                columns: new[] { "SchoolId", "CredentialVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EstablishmentCredential_Active",
                table: "BootstrapSchoolEstablishmentCredentials",
                column: "SchoolId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_BootstrapSchoolRegistry_IsActive",
                table: "BootstrapSchoolRegistry",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_BootstrapSchoolRegistry_SchoolId",
                table: "BootstrapSchoolRegistry",
                column: "SchoolId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BootstrapEstablishmentSessions");

            migrationBuilder.DropTable(
                name: "BootstrapSchoolEstablishmentCredentials");

            migrationBuilder.DropTable(
                name: "BootstrapSchoolRegistry");
        }
    }
}
