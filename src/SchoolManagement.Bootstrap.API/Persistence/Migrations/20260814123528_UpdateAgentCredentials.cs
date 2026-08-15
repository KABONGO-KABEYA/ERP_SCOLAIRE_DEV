using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Bootstrap.API.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAgentCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdateAgentCredential",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
                    SecretHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateAgentCredential", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpdateAgentCredential_BootstrapSchoolRegistry_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "BootstrapSchoolRegistry",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpdateAgentCredential_SchoolId_Version",
                table: "UpdateAgentCredential",
                columns: new[] { "SchoolId", "CredentialVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UpdateAgentCredential_Active",
                table: "UpdateAgentCredential",
                column: "SchoolId",
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateAgentCredential");
        }
    }
}
