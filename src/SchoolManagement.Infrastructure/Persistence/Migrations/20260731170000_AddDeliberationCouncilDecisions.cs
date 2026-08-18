using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SchoolDbContext))]
[Migration("20260731170000_AddDeliberationCouncilDecisions")]
public partial class AddDeliberationCouncilDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeliberationDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClassRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProposedDecision = table.Column<int>(type: "int", nullable: false),
                FinalDecision = table.Column<int>(type: "int", nullable: false),
                Observation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DecidedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                table.PrimaryKey("PK_DeliberationDecisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeliberationDecisions_AcademicPeriods_AcademicPeriodId",
                    column: x => x.AcademicPeriodId,
                    principalTable: "AcademicPeriods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DeliberationDecisions_AcademicYears_AcademicYearId",
                    column: x => x.AcademicYearId,
                    principalTable: "AcademicYears",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DeliberationDecisions_ClassRooms_ClassRoomId",
                    column: x => x.ClassRoomId,
                    principalTable: "ClassRooms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DeliberationDecisions_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DeliberationDecisions_Students_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DeliberationDecisionEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProposedDecision = table.Column<int>(type: "int", nullable: false),
                FinalDecision = table.Column<int>(type: "int", nullable: false),
                Observation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                table.PrimaryKey("PK_DeliberationDecisionEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeliberationDecisionEvents_DeliberationDecisions_DecisionId",
                    column: x => x.DecisionId,
                    principalTable: "DeliberationDecisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StudentRemedialSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClassRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SessionKind = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_StudentRemedialSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_StudentRemedialSessions_DeliberationDecisions_DecisionId",
                    column: x => x.DecisionId,
                    principalTable: "DeliberationDecisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StudentRemedialSessions_Students_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StudentRemedialCourses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RemedialSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_StudentRemedialCourses", x => x.Id);
                table.ForeignKey(
                    name: "FK_StudentRemedialCourses_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StudentRemedialCourses_StudentRemedialSessions_RemedialSessionId",
                    column: x => x.RemedialSessionId,
                    principalTable: "StudentRemedialSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CourseExemptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Motive = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Observation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                table.PrimaryKey("PK_CourseExemptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_CourseExemptions_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CourseExemptions_DeliberationDecisions_DecisionId",
                    column: x => x.DecisionId,
                    principalTable: "DeliberationDecisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CourseExemptions_Students_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeliberationDecisions_Scope_Student",
            table: "DeliberationDecisions",
            columns: new[] { "SchoolId", "AcademicYearId", "ClassRoomId", "AcademicPeriodId", "StudentId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_DeliberationDecisionEvents_DecisionId_OccurredAtUtc",
            table: "DeliberationDecisionEvents",
            columns: new[] { "DecisionId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_StudentRemedialSessions_DecisionId",
            table: "StudentRemedialSessions",
            column: "DecisionId",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_StudentRemedialCourses_Session_Course",
            table: "StudentRemedialCourses",
            columns: new[] { "RemedialSessionId", "CourseId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_CourseExemptions_Decision_Course",
            table: "CourseExemptions",
            columns: new[] { "DecisionId", "CourseId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CourseExemptions");
        migrationBuilder.DropTable(name: "StudentRemedialCourses");
        migrationBuilder.DropTable(name: "StudentRemedialSessions");
        migrationBuilder.DropTable(name: "DeliberationDecisionEvents");
        migrationBuilder.DropTable(name: "DeliberationDecisions");
    }
}
