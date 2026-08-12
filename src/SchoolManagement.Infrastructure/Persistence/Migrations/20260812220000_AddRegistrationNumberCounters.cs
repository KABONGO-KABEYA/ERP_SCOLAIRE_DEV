using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// P4 — table compteur matricules + initialisation depuis Students (soft-deleted inclus).
/// </summary>
public partial class AddRegistrationNumberCounters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RegistrationNumberCounters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                NextValue = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistrationNumberCounters", x => x.Id);
                table.ForeignKey(
                    name: "FK_RegistrationNumberCounters_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RegistrationNumberCounters_IsDeleted",
            table: "RegistrationNumberCounters",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_RegistrationNumberCounters_SchoolId_Year",
            table: "RegistrationNumberCounters",
            columns: new[] { "SchoolId", "Year" },
            unique: true);

        // Seed : NextValue = MAX(séquence) + 1 par SchoolId + année, y compris soft-deleted.
        // Formats non ELV-YYYY-n… ignorés (ex. anomalies signalées hors migration).
        migrationBuilder.Sql("""
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            SELECT
                NEWID(),
                parsed.SchoolId,
                parsed.Yr,
                MAX(parsed.Seq) + 1,
                SYSUTCDATETIME(),
                0
            FROM (
                SELECT
                    s.SchoolId,
                    TRY_CAST(SUBSTRING(s.RegistrationNumber, 5, 4) AS int) AS Yr,
                    TRY_CAST(SUBSTRING(s.RegistrationNumber, 10, LEN(s.RegistrationNumber) - 9) AS int) AS Seq
                FROM Students s
                WHERE s.RegistrationNumber LIKE 'ELV-[0-9][0-9][0-9][0-9]-%'
            ) parsed
            WHERE parsed.Yr IS NOT NULL
              AND parsed.Seq IS NOT NULL
              AND parsed.Seq > 0
            GROUP BY parsed.SchoolId, parsed.Yr;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RegistrationNumberCounters");
    }
}
