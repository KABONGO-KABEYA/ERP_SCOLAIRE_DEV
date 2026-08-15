using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Bootstrap.API.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReleaseCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdateRelease",
                columns: table => new
                {
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    MinimumDesktopVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MinimumApiVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mandatory = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateRelease", x => x.ReleaseId);
                    table.CheckConstraint("CK_UpdateRelease_Channel", "[Channel] IN (N'DEV', N'PROD')");
                    table.CheckConstraint("CK_UpdateRelease_Status", "[Status] IN (N'Draft', N'Published', N'Blocked')");
                });

            migrationBuilder.CreateTable(
                name: "UpdateReleaseArtifact",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateReleaseArtifact", x => x.ArtifactId);
                    table.CheckConstraint("CK_UpdateReleaseArtifact_Sha256", "LEN([Sha256]) = 64 AND [Sha256] NOT LIKE N'%[^0-9a-f]%'");
                    table.CheckConstraint("CK_UpdateReleaseArtifact_Type", "[Type] IN (N'Desktop', N'Api', N'Migration', N'Mobile')");
                    table.ForeignKey(
                        name: "FK_UpdateReleaseArtifact_UpdateRelease_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "UpdateRelease",
                        principalColumn: "ReleaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UpdateReleaseTarget",
                columns: table => new
                {
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateReleaseTarget", x => x.TargetId);
                    table.ForeignKey(
                        name: "FK_UpdateReleaseTarget_BootstrapSchoolRegistry_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "BootstrapSchoolRegistry",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpdateReleaseTarget_UpdateRelease_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "UpdateRelease",
                        principalColumn: "ReleaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpdateRelease_Channel_Status",
                table: "UpdateRelease",
                columns: new[] { "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_UpdateRelease_Channel_Version",
                table: "UpdateRelease",
                columns: new[] { "Channel", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UpdateReleaseArtifact_Release_Type",
                table: "UpdateReleaseArtifact",
                columns: new[] { "ReleaseId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpdateReleaseTarget_SchoolId",
                table: "UpdateReleaseTarget",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "UX_UpdateReleaseTarget_Release_Global",
                table: "UpdateReleaseTarget",
                column: "ReleaseId",
                unique: true,
                filter: "[SchoolId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UpdateReleaseTarget_Release_School",
                table: "UpdateReleaseTarget",
                columns: new[] { "ReleaseId", "SchoolId" },
                unique: true,
                filter: "[SchoolId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateReleaseArtifact");

            migrationBuilder.DropTable(
                name: "UpdateReleaseTarget");

            migrationBuilder.DropTable(
                name: "UpdateRelease");
        }
    }
}
