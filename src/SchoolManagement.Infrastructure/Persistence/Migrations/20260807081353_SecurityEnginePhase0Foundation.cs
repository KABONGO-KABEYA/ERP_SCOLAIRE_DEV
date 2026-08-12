using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
/// <remarks>
/// Phase 0 sécurité uniquement. Le Designer/Snapshot reflètent le modèle complet actuel ;
/// ce Up/Down n'applique volontairement que les objets catalogue / exceptions / audit
/// pour éviter le drift destructif hors périmètre.
/// </remarks>
    public partial class SecurityEnginePhase0Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformSuperAdmin",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAssignable",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
            name: "DisplayName",
                table: "Permissions",
            type: "nvarchar(150)",
            maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
            name: "BusinessDescription",
                table: "Permissions",
            type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HelpText",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Permissions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityActionId",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: true);

        migrationBuilder.Sql("""
            UPDATE Permissions
            SET DisplayName = CASE WHEN DisplayName = N'' OR DisplayName IS NULL THEN Code ELSE DisplayName END,
                BusinessDescription = CASE
                    WHEN BusinessDescription = N'' OR BusinessDescription IS NULL THEN ISNULL(Description, Code)
                    ELSE BusinessDescription END,
                HelpText = COALESCE(HelpText, Description, Code),
                IsActive = 1;
            """);

            migrationBuilder.CreateTable(
            name: "SecurityModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
            constraints: table => table.PrimaryKey("PK_SecurityModules", x => x.Id));

            migrationBuilder.CreateTable(
            name: "SecurityFunctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                table.PrimaryKey("PK_SecurityFunctions", x => x.Id);
                    table.ForeignKey(
                    name: "FK_SecurityFunctions_SecurityModules_ModuleId",
                    column: x => x.ModuleId,
                    principalTable: "SecurityModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
            name: "SecurityPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FunctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                RequiredPermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DesktopViewKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                WebRoute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                MobileScreenKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                DeepLink = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                IsAvailableOnDesktop = table.Column<bool>(type: "bit", nullable: false),
                IsAvailableOnWeb = table.Column<bool>(type: "bit", nullable: false),
                IsAvailableOnMobile = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_SecurityPages", x => x.Id);
                    table.ForeignKey(
                    name: "FK_SecurityPages_SecurityFunctions_FunctionId",
                    column: x => x.FunctionId,
                    principalTable: "SecurityFunctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
            name: "SecurityActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                IsAvailableOnDesktop = table.Column<bool>(type: "bit", nullable: false),
                IsAvailableOnWeb = table.Column<bool>(type: "bit", nullable: false),
                IsAvailableOnMobile = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_SecurityActions", x => x.Id);
                    table.ForeignKey(
                    name: "FK_SecurityActions_SecurityPages_PageId",
                    column: x => x.PageId,
                    principalTable: "SecurityPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermissionDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiresPermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_PermissionDependencies", x => x.Id);
                    table.CheckConstraint("CK_PermissionDependencies_NoSelf", "[PermissionId] <> [RequiresPermissionId]");
                    table.ForeignKey(
                        name: "FK_PermissionDependencies_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissionDependencies_Permissions_RequiresPermissionId",
                        column: x => x.RequiresPermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorKind = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SecurityAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityAuditLogs_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissionExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_UserPermissionExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissionExceptions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissionExceptions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissionExceptions_UserAccounts_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissionExceptions_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

        migrationBuilder.CreateIndex(name: "IX_Permissions_SecurityActionId", table: "Permissions", column: "SecurityActionId");
        migrationBuilder.CreateIndex(name: "IX_SecurityModules_Code", table: "SecurityModules", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SecurityModules_IsDeleted", table: "SecurityModules", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_SecurityFunctions_IsDeleted", table: "SecurityFunctions", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_SecurityFunctions_ModuleId_Code", table: "SecurityFunctions", columns: new[] { "ModuleId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_SecurityPages_DesktopViewKey", table: "SecurityPages", column: "DesktopViewKey");
        migrationBuilder.CreateIndex(name: "IX_SecurityPages_FunctionId_Code", table: "SecurityPages", columns: new[] { "FunctionId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_SecurityPages_IsDeleted", table: "SecurityPages", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_SecurityActions_IsDeleted", table: "SecurityActions", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_SecurityActions_PageId_Code", table: "SecurityActions", columns: new[] { "PageId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_PermissionDependencies_IsDeleted", table: "PermissionDependencies", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_PermissionDependencies_PermissionId_RequiresPermissionId", table: "PermissionDependencies", columns: new[] { "PermissionId", "RequiresPermissionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_PermissionDependencies_RequiresPermissionId", table: "PermissionDependencies", column: "RequiresPermissionId");
        migrationBuilder.CreateIndex(name: "IX_SecurityAuditLogs_ActionType_OccurredAtUtc", table: "SecurityAuditLogs", columns: new[] { "ActionType", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_SecurityAuditLogs_IsDeleted", table: "SecurityAuditLogs", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_SecurityAuditLogs_SchoolId_OccurredAtUtc", table: "SecurityAuditLogs", columns: new[] { "SchoolId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_UserPermissionExceptions_GrantedByUserId", table: "UserPermissionExceptions", column: "GrantedByUserId");
        migrationBuilder.CreateIndex(name: "IX_UserPermissionExceptions_IsDeleted", table: "UserPermissionExceptions", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_UserPermissionExceptions_PermissionId", table: "UserPermissionExceptions", column: "PermissionId");
        migrationBuilder.CreateIndex(name: "IX_UserPermissionExceptions_SchoolId_UserId", table: "UserPermissionExceptions", columns: new[] { "SchoolId", "UserId" });
        migrationBuilder.CreateIndex(name: "IX_UserPermissionExceptions_UserId_PermissionId_Effect_ValidFrom_ValidTo", table: "UserPermissionExceptions", columns: new[] { "UserId", "PermissionId", "Effect", "ValidFrom", "ValidTo" });

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_SecurityActions_SecurityActionId",
                table: "Permissions",
                column: "SecurityActionId",
                principalTable: "SecurityActions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_SecurityActions_SecurityActionId",
                table: "Permissions");

        migrationBuilder.DropTable(name: "PermissionDependencies");
        migrationBuilder.DropTable(name: "UserPermissionExceptions");
        migrationBuilder.DropTable(name: "SecurityAuditLogs");
        migrationBuilder.DropTable(name: "SecurityActions");
        migrationBuilder.DropTable(name: "SecurityPages");
        migrationBuilder.DropTable(name: "SecurityFunctions");
        migrationBuilder.DropTable(name: "SecurityModules");

        migrationBuilder.DropIndex(name: "IX_Permissions_SecurityActionId", table: "Permissions");

        migrationBuilder.DropColumn(name: "IsPlatformSuperAdmin", table: "UserAccounts");
        migrationBuilder.DropColumn(name: "IsAssignable", table: "Roles");
        migrationBuilder.DropColumn(name: "IsSystem", table: "Roles");
        migrationBuilder.DropColumn(name: "SortOrder", table: "Roles");
        migrationBuilder.DropColumn(name: "BusinessDescription", table: "Permissions");
        migrationBuilder.DropColumn(name: "DisplayName", table: "Permissions");
        migrationBuilder.DropColumn(name: "HelpText", table: "Permissions");
        migrationBuilder.DropColumn(name: "IsActive", table: "Permissions");
        migrationBuilder.DropColumn(name: "SecurityActionId", table: "Permissions");
    }
}
