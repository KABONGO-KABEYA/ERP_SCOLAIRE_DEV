using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Bootstrap.API.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReleaseFromSchemaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromSchemaVersion",
                table: "UpdateRelease",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE dbo.UpdateRelease SET FromSchemaVersion = 1 WHERE FromSchemaVersion < 1;
                UPDATE dbo.UpdateRelease SET SchemaVersion = FromSchemaVersion WHERE SchemaVersion < FromSchemaVersion;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UpdateRelease_SchemaRange",
                table: "UpdateRelease",
                sql: "[FromSchemaVersion] >= 1 AND [SchemaVersion] >= [FromSchemaVersion]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UpdateRelease_SchemaRange",
                table: "UpdateRelease");

            migrationBuilder.DropColumn(
                name: "FromSchemaVersion",
                table: "UpdateRelease");
        }
    }
}
