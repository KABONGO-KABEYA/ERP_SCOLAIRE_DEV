using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SchoolDbContext))]
[Migration("20260804120000_AddParentActivation")]
public partial class AddParentActivation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ParentActivationTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                SuggestedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ParentActivationTokens", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ParentActivationSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActivationTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                BootstrapSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ParentActivationSessions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ParentActivationTokens_IsDeleted",
            table: "ParentActivationTokens",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ParentActivationTokens_SchoolId",
            table: "ParentActivationTokens",
            column: "SchoolId");

        migrationBuilder.CreateIndex(
            name: "IX_ParentActivationSessions_ActivationTokenId",
            table: "ParentActivationSessions",
            column: "ActivationTokenId");

        migrationBuilder.CreateIndex(
            name: "IX_ParentActivationSessions_IsDeleted",
            table: "ParentActivationSessions",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ParentActivationSessions_SchoolId",
            table: "ParentActivationSessions",
            column: "SchoolId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ParentActivationSessions");
        migrationBuilder.DropTable(name: "ParentActivationTokens");
    }
}
