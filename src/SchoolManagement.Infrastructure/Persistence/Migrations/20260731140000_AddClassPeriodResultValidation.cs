using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SchoolDbContext))]
[Migration("20260731140000_AddClassPeriodResultValidation")]
public partial class AddClassPeriodResultValidation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClassPeriodResultValidations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClassRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ValidatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ValidatedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LockedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                Observations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_ClassPeriodResultValidations", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClassPeriodResultValidations_AcademicPeriods_AcademicPeriodId",
                    column: x => x.AcademicPeriodId,
                    principalTable: "AcademicPeriods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodResultValidations_AcademicYears_AcademicYearId",
                    column: x => x.AcademicYearId,
                    principalTable: "AcademicYears",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodResultValidations_ClassRooms_ClassRoomId",
                    column: x => x.ClassRoomId,
                    principalTable: "ClassRooms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClassPeriodResultValidations_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ClassPeriodResultValidationEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Operation = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Observations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_ClassPeriodResultValidationEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClassPeriodResultValidationEvents_ClassPeriodResultValidations_ValidationId",
                    column: x => x.ValidationId,
                    principalTable: "ClassPeriodResultValidations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidations_AcademicPeriodId",
            table: "ClassPeriodResultValidations",
            column: "AcademicPeriodId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidations_AcademicYearId",
            table: "ClassPeriodResultValidations",
            column: "AcademicYearId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidations_ClassRoomId",
            table: "ClassPeriodResultValidations",
            column: "ClassRoomId");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidations_IsDeleted",
            table: "ClassPeriodResultValidations",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidations_SchoolId_AcademicYearId_ClassRoomId_AcademicPeriodId",
            table: "ClassPeriodResultValidations",
            columns: new[] { "SchoolId", "AcademicYearId", "ClassRoomId", "AcademicPeriodId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidationEvents_IsDeleted",
            table: "ClassPeriodResultValidationEvents",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_ClassPeriodResultValidationEvents_ValidationId_OccurredAtUtc",
            table: "ClassPeriodResultValidationEvents",
            columns: new[] { "ValidationId", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClassPeriodResultValidationEvents");
        migrationBuilder.DropTable(name: "ClassPeriodResultValidations");
    }
}
