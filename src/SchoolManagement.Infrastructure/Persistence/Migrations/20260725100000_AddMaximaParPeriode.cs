using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

public partial class AddMaximaParPeriode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MaximaParPeriode",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PedagogicalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Maximum = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_MaximaParPeriode", x => x.Id);
                table.ForeignKey(
                    name: "FK_MaximaParPeriode_AcademicPeriods_AcademicPeriodId",
                    column: x => x.AcademicPeriodId,
                    principalTable: "AcademicPeriods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaximaParPeriode_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaximaParPeriode_PedagogicalClasses_PedagogicalClassId",
                    column: x => x.PedagogicalClassId,
                    principalTable: "PedagogicalClasses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MaximaParPeriode_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MaximaParPeriode_AcademicPeriodId",
            table: "MaximaParPeriode",
            column: "AcademicPeriodId");

        migrationBuilder.CreateIndex(
            name: "IX_MaximaParPeriode_IsDeleted",
            table: "MaximaParPeriode",
            column: "IsDeleted");

        migrationBuilder.CreateIndex(
            name: "IX_MaximaParPeriode_PedagogicalClassId_CourseId_AcademicPeriodId",
            table: "MaximaParPeriode",
            columns: new[] { "PedagogicalClassId", "CourseId", "AcademicPeriodId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MaximaParPeriode");
    }
}
