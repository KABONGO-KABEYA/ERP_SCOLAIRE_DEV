using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

/// <summary>Isolation multi-tenant : SchoolId sur périodes, sync, audit, caisse.</summary>
[DbContext(typeof(SchoolDbContext))]
[Migration("20260805103000_StrictSchoolTenantIsolation")]
public partial class StrictSchoolTenantIsolation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "AcademicPeriods",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE ap
            SET ap.SchoolId = ay.SchoolId
            FROM AcademicPeriods ap
            INNER JOIN AcademicYears ay ON ay.Id = ap.AcademicYearId
            WHERE ap.SchoolId IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "AcademicPeriods",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "SyncWatermark",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "SyncJournal",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "AuditEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "LoginHistory",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SchoolId",
            table: "CashMovements",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("""
            DECLARE @DefaultSchool uniqueidentifier = (SELECT TOP 1 Id FROM Schools WHERE IsDeleted = 0 ORDER BY CreatedAt);

            UPDATE SyncWatermark SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
            UPDATE SyncJournal SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
            UPDATE AuditEntries SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
            UPDATE LoginHistory SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;

            UPDATE cm
            SET cm.SchoolId = p.SchoolId
            FROM CashMovements cm
            INNER JOIN Payments p ON p.Id = cm.PaymentId
            WHERE cm.SchoolId IS NULL AND cm.PaymentId IS NOT NULL;

            UPDATE CashMovements SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;

            UPDATE SyncOutboxUnit SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "SyncOutboxUnit",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "SyncWatermark",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "SyncJournal",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "AuditEntries",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "LoginHistory",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "CashMovements",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_SyncWatermark_TableName",
            table: "SyncWatermark");

        migrationBuilder.CreateIndex(
            name: "IX_AcademicPeriods_SchoolId_AcademicYearId_MainPeriodId_OrderIndex",
            table: "AcademicPeriods",
            columns: new[] { "SchoolId", "AcademicYearId", "MainPeriodId", "OrderIndex" });

        migrationBuilder.CreateIndex(
            name: "IX_AcademicPeriods_SchoolId",
            table: "AcademicPeriods",
            column: "SchoolId");

        migrationBuilder.CreateIndex(
            name: "IX_SyncOutboxUnit_SchoolId_Status_Priority_CreatedAt",
            table: "SyncOutboxUnit",
            columns: new[] { "SchoolId", "Status", "Priority", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SyncWatermark_SchoolId_TableName",
            table: "SyncWatermark",
            columns: new[] { "SchoolId", "TableName" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_SyncJournal_SchoolId_StartedAt",
            table: "SyncJournal",
            columns: new[] { "SchoolId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_SchoolId_Timestamp",
            table: "AuditEntries",
            columns: new[] { "SchoolId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_LoginHistory_SchoolId_LoginAt",
            table: "LoginHistory",
            columns: new[] { "SchoolId", "LoginAt" });

        migrationBuilder.CreateIndex(
            name: "IX_CashMovements_SchoolId_CashRegisterId_MovementDate",
            table: "CashMovements",
            columns: new[] { "SchoolId", "CashRegisterId", "MovementDate" });

        migrationBuilder.AddForeignKey(
            name: "FK_AcademicPeriods_Schools_SchoolId",
            table: "AcademicPeriods",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AuditEntries_Schools_SchoolId",
            table: "AuditEntries",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_CashMovements_Schools_SchoolId",
            table: "CashMovements",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_LoginHistory_Schools_SchoolId",
            table: "LoginHistory",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SyncJournal_Schools_SchoolId",
            table: "SyncJournal",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SyncOutboxUnit_Schools_SchoolId",
            table: "SyncOutboxUnit",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SyncWatermark_Schools_SchoolId",
            table: "SyncWatermark",
            column: "SchoolId",
            principalTable: "Schools",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_AcademicPeriods_Schools_SchoolId", table: "AcademicPeriods");
        migrationBuilder.DropForeignKey(name: "FK_AuditEntries_Schools_SchoolId", table: "AuditEntries");
        migrationBuilder.DropForeignKey(name: "FK_CashMovements_Schools_SchoolId", table: "CashMovements");
        migrationBuilder.DropForeignKey(name: "FK_LoginHistory_Schools_SchoolId", table: "LoginHistory");
        migrationBuilder.DropForeignKey(name: "FK_SyncJournal_Schools_SchoolId", table: "SyncJournal");
        migrationBuilder.DropForeignKey(name: "FK_SyncOutboxUnit_Schools_SchoolId", table: "SyncOutboxUnit");
        migrationBuilder.DropForeignKey(name: "FK_SyncWatermark_Schools_SchoolId", table: "SyncWatermark");

        migrationBuilder.DropIndex(name: "IX_AcademicPeriods_SchoolId_AcademicYearId_MainPeriodId_OrderIndex", table: "AcademicPeriods");
        migrationBuilder.DropIndex(name: "IX_AcademicPeriods_SchoolId", table: "AcademicPeriods");
        migrationBuilder.DropIndex(name: "IX_SyncOutboxUnit_SchoolId_Status_Priority_CreatedAt", table: "SyncOutboxUnit");
        migrationBuilder.DropIndex(name: "IX_SyncWatermark_SchoolId_TableName", table: "SyncWatermark");
        migrationBuilder.DropIndex(name: "IX_SyncJournal_SchoolId_StartedAt", table: "SyncJournal");
        migrationBuilder.DropIndex(name: "IX_AuditEntries_SchoolId_Timestamp", table: "AuditEntries");
        migrationBuilder.DropIndex(name: "IX_LoginHistory_SchoolId_LoginAt", table: "LoginHistory");
        migrationBuilder.DropIndex(name: "IX_CashMovements_SchoolId_CashRegisterId_MovementDate", table: "CashMovements");

        migrationBuilder.DropColumn(name: "SchoolId", table: "AcademicPeriods");
        migrationBuilder.DropColumn(name: "SchoolId", table: "SyncWatermark");
        migrationBuilder.DropColumn(name: "SchoolId", table: "SyncJournal");
        migrationBuilder.DropColumn(name: "SchoolId", table: "AuditEntries");
        migrationBuilder.DropColumn(name: "SchoolId", table: "LoginHistory");
        migrationBuilder.DropColumn(name: "SchoolId", table: "CashMovements");

        migrationBuilder.AlterColumn<Guid>(
            name: "SchoolId",
            table: "SyncOutboxUnit",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.CreateIndex(
            name: "IX_SyncWatermark_TableName",
            table: "SyncWatermark",
            column: "TableName",
            unique: true,
            filter: "[IsDeleted] = 0");
    }
}
