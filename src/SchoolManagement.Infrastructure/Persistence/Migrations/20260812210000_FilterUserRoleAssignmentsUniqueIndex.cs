using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Aligne l'unicité UserRoleAssignments sur le soft-delete :
/// UNIQUE(UserId, RoleId) WHERE IsDeleted = 0.
/// Permet de réactiver une ligne soft-deleted sans SQL 2601.
/// </summary>
[DbContext(typeof(SchoolDbContext))]
[Migration("20260812210000_FilterUserRoleAssignmentsUniqueIndex")]
public partial class FilterUserRoleAssignmentsUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserRoleAssignments_UserId_RoleId",
            table: "UserRoleAssignments");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_UserId_RoleId",
            table: "UserRoleAssignments",
            columns: new[] { "UserId", "RoleId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserRoleAssignments_UserId_RoleId",
            table: "UserRoleAssignments");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_UserId_RoleId",
            table: "UserRoleAssignments",
            columns: new[] { "UserId", "RoleId" },
            unique: true);
    }
}
