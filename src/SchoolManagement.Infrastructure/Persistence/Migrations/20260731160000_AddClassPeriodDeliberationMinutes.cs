using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

public partial class AddClassPeriodDeliberationMinutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClassPeriodDeliberationMinutes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClassRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GeneralObservations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CouncilDecisions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PedagogicalRecommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                table.PrimaryKey("PK_ClassPeriodDeliberationMinutes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClassPeriodDeliberationMinutes_AcademicPeriods_AcademicPeriodId",
                    column: x => x.AcademicPeriodId,
                    principalTable: "AcademicPeriods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodDeliberationMinutes_AcademicYears_AcademicYearId",
                    column: x => x.AcademicYearId,
                    principalTable: "AcademicYears",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodDeliberationMinutes_ClassRooms_ClassRoomId",
                    column: x => x.ClassRoomId,
                    principalTable: "ClassRooms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodDeliberationMinutes_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodDeliberationMinutes_AcademicPeriodId",
            table: "ClassPeriodDeliberationMinutes",
            column: "AcademicPeriodId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodDeliberationMinutes_AcademicYearId",
            table: "ClassPeriodDeliberationMinutes",
            column: "AcademicYearId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodDeliberationMinutes_ClassRoomId",
            table: "ClassPeriodDeliberationMinutes",
            column: "ClassRoomId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodDeliberationMinutes_IsDeleted",
            table: "ClassPeriodDeliberationMinutes",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodDeliberationMinutes_SchoolId_AcademicYearId_ClassRoomId_AcademicPeriodId",
            table: "ClassPeriodDeliberationMinutes",
            columns: new[] { "SchoolId", "AcademicYearId", "ClassRoomId", "AcademicPeriodId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClassPeriodDeliberationMinutes");
    }
}
