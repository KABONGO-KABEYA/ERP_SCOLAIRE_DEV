using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SchoolDbContext))]
[Migration("20260723140000_AddBranchAndPedagogicalClassCourse")]
public partial class AddBranchAndPedagogicalClassCourse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Branches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Program = table.Column<int>(type: "int", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_Branches", x => x.Id);
                table.ForeignKey(
                    name: "FK_Branches_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "BranchId",
            table: "Courses",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PedagogicalClassCourses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PedagogicalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_PedagogicalClassCourses", x => x.Id);
                table.ForeignKey(
                    name: "FK_PedagogicalClassCourses_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PedagogicalClassCourses_PedagogicalClasses_PedagogicalClassId",
                    column: x => x.PedagogicalClassId,
                    principalTable: "PedagogicalClasses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PedagogicalClassCourses_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Branches_IsDeleted",
            table: "Branches",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_Branches_SchoolId_Code",
            table: "Branches",
            columns: new[] { "SchoolId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Courses_BranchId",
            table: "Courses",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_Courses_SchoolId_Code_ClassRoomId",
            table: "Courses",
            columns: new[] { "SchoolId", "Code", "ClassRoomId" },
            unique: true,
            filter: "[ClassRoomId] IS NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_PedagogicalClassCourses_CourseId",
            table: "PedagogicalClassCourses",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_PedagogicalClassCourses_IsDeleted",
            table: "PedagogicalClassCourses",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_PedagogicalClassCourses_PedagogicalClassId_CourseId",
            table: "PedagogicalClassCourses",
            columns: new[] { "PedagogicalClassId", "CourseId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Branches_BranchId",
            table: "Courses",
            column: "BranchId",
            principalTable: "Branches",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Branches_BranchId",
            table: "Courses");

        migrationBuilder.DropTable(
            name: "PedagogicalClassCourses");

        migrationBuilder.DropTable(
            name: "Branches");

        migrationBuilder.DropIndex(
            name: "IX_Courses_BranchId",
            table: "Courses");

        migrationBuilder.DropIndex(
            name: "IX_Courses_SchoolId_Code_ClassRoomId",
            table: "Courses");

        migrationBuilder.DropColumn(
            name: "BranchId",
            table: "Courses");
    }
}
