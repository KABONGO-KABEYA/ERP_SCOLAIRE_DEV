using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPedagogicalStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeacherAttendances_TeacherId_AttendanceDate",
                table: "TeacherAttendances");

            migrationBuilder.DropIndex(
                name: "IX_StudentAttendances_StudentId_AttendanceDate",
                table: "StudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_DisciplineRecords_StudentId_IncidentDate",
                table: "DisciplineRecords");

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "TeacherAttendances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "HumanitiesSection",
                table: "StudyOptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "StudentAttendances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "MeritRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "DisciplineRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ClassRooms",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ClassRooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Observations",
                table: "ClassRooms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PedagogicalClassId",
                table: "ClassRooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PedagogicalClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Program = table.Column<int>(type: "int", nullable: false),
                    LevelOrder = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HumanitiesSection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StudyOption = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PedagogicalClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedagogicalClasses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendances_SchoolId_TeacherId_AttendanceDate",
                table: "TeacherAttendances",
                columns: new[] { "SchoolId", "TeacherId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendances_TeacherId",
                table: "TeacherAttendances",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_SchoolId_StudentId_AttendanceDate",
                table: "StudentAttendances",
                columns: new[] { "SchoolId", "StudentId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_StudentId",
                table: "StudentAttendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodResults_AcademicYearId",
                table: "PeriodResults",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_MeritRecords_SchoolId_StudentId_AwardDate",
                table: "MeritRecords",
                columns: new[] { "SchoolId", "StudentId", "AwardDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineRecords_SchoolId_StudentId_IncidentDate",
                table: "DisciplineRecords",
                columns: new[] { "SchoolId", "StudentId", "IncidentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineRecords_StudentId",
                table: "DisciplineRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRooms_PedagogicalClassId_AcademicYearId_Name",
                table: "ClassRooms",
                columns: new[] { "PedagogicalClassId", "AcademicYearId", "Name" },
                unique: true,
                filter: "[PedagogicalClassId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_AcademicYearId",
                table: "CalendarEvents",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_PedagogicalClasses_IsDeleted",
                table: "PedagogicalClasses",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PedagogicalClasses_SchoolId_IsEnabled",
                table: "PedagogicalClasses",
                columns: new[] { "SchoolId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_PedagogicalClasses_SchoolId_TemplateCode",
                table: "PedagogicalClasses",
                columns: new[] { "SchoolId", "TemplateCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Schools_SchoolId",
                table: "Announcements",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_AcademicYears_AcademicYearId",
                table: "CalendarEvents",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Schools_SchoolId",
                table: "CalendarEvents",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassRooms_PedagogicalClasses_PedagogicalClassId",
                table: "ClassRooms",
                column: "PedagogicalClassId",
                principalTable: "PedagogicalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodResults_AcademicYears_AcademicYearId",
                table: "PeriodResults",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodResults_ClassRooms_ClassRoomId",
                table: "PeriodResults",
                column: "ClassRoomId",
                principalTable: "ClassRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Schools_SchoolId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_AcademicYears_AcademicYearId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Schools_SchoolId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassRooms_PedagogicalClasses_PedagogicalClassId",
                table: "ClassRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodResults_AcademicYears_AcademicYearId",
                table: "PeriodResults");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodResults_ClassRooms_ClassRoomId",
                table: "PeriodResults");

            migrationBuilder.DropTable(
                name: "PedagogicalClasses");

            migrationBuilder.DropIndex(
                name: "IX_TeacherAttendances_SchoolId_TeacherId_AttendanceDate",
                table: "TeacherAttendances");

            migrationBuilder.DropIndex(
                name: "IX_TeacherAttendances_TeacherId",
                table: "TeacherAttendances");

            migrationBuilder.DropIndex(
                name: "IX_StudentAttendances_SchoolId_StudentId_AttendanceDate",
                table: "StudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_StudentAttendances_StudentId",
                table: "StudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_PeriodResults_AcademicYearId",
                table: "PeriodResults");

            migrationBuilder.DropIndex(
                name: "IX_MeritRecords_SchoolId_StudentId_AwardDate",
                table: "MeritRecords");

            migrationBuilder.DropIndex(
                name: "IX_DisciplineRecords_SchoolId_StudentId_IncidentDate",
                table: "DisciplineRecords");

            migrationBuilder.DropIndex(
                name: "IX_DisciplineRecords_StudentId",
                table: "DisciplineRecords");

            migrationBuilder.DropIndex(
                name: "IX_ClassRooms_PedagogicalClassId_AcademicYearId_Name",
                table: "ClassRooms");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_AcademicYearId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "TeacherAttendances");

            migrationBuilder.DropColumn(
                name: "HumanitiesSection",
                table: "StudyOptions");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "StudentAttendances");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "MeritRecords");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "DisciplineRecords");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ClassRooms");

            migrationBuilder.DropColumn(
                name: "Observations",
                table: "ClassRooms");

            migrationBuilder.DropColumn(
                name: "PedagogicalClassId",
                table: "ClassRooms");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ClassRooms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAttendances_TeacherId_AttendanceDate",
                table: "TeacherAttendances",
                columns: new[] { "TeacherId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_StudentId_AttendanceDate",
                table: "StudentAttendances",
                columns: new[] { "StudentId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineRecords_StudentId_IncidentDate",
                table: "DisciplineRecords",
                columns: new[] { "StudentId", "IncidentDate" });
        }
    }
}
