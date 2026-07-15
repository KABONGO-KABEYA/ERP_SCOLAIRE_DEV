using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

public partial class AddDocumentBranding : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EcoleLogo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_EcoleLogo", x => x.Id);
                table.ForeignKey(
                    name: "FK_EcoleLogo_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EcoleEntete",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                DocumentType = table.Column<int>(type: "int", nullable: false),
                PrintMode = table.Column<int>(type: "int", nullable: false),
                ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                WidthPx = table.Column<int>(type: "int", nullable: true),
                HeightPx = table.Column<int>(type: "int", nullable: true),
                ResolutionDpi = table.Column<int>(type: "int", nullable: true),
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
                table.PrimaryKey("PK_EcoleEntete", x => x.Id);
                table.ForeignKey(
                    name: "FK_EcoleEntete_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EcoleSignature",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SignatoryName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Function = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                table.PrimaryKey("PK_EcoleSignature", x => x.Id);
                table.ForeignKey(
                    name: "FK_EcoleSignature_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EcoleCachet",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                table.PrimaryKey("PK_EcoleCachet", x => x.Id);
                table.ForeignKey(
                    name: "FK_EcoleCachet_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EcolePiedPage",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                PoBox = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SchoolMotto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                FreeText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                table.PrimaryKey("PK_EcolePiedPage", x => x.Id);
                table.ForeignKey(
                    name: "FK_EcolePiedPage_Schools_SchoolId",
                    column: x => x.SchoolId,
                    principalTable: "Schools",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_EcoleLogo_IsDeleted", table: "EcoleLogo", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_EcoleLogo_SchoolId_IsPrimary", table: "EcoleLogo", columns: new[] { "SchoolId", "IsPrimary" });
        migrationBuilder.CreateIndex(name: "IX_EcoleLogo_SchoolId_Name", table: "EcoleLogo", columns: new[] { "SchoolId", "Name" });

        migrationBuilder.CreateIndex(name: "IX_EcoleEntete_IsDeleted", table: "EcoleEntete", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_EcoleEntete_SchoolId_DocumentType_Name", table: "EcoleEntete", columns: new[] { "SchoolId", "DocumentType", "Name" });

        migrationBuilder.CreateIndex(name: "IX_EcoleSignature_IsDeleted", table: "EcoleSignature", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_EcoleSignature_SchoolId_Function", table: "EcoleSignature", columns: new[] { "SchoolId", "Function" });

        migrationBuilder.CreateIndex(name: "IX_EcoleCachet_IsDeleted", table: "EcoleCachet", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_EcoleCachet_SchoolId_Name", table: "EcoleCachet", columns: new[] { "SchoolId", "Name" });

        migrationBuilder.CreateIndex(name: "IX_EcolePiedPage_IsDeleted", table: "EcolePiedPage", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_EcolePiedPage_SchoolId", table: "EcolePiedPage", column: "SchoolId", unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EcolePiedPage");
        migrationBuilder.DropTable(name: "EcoleCachet");
        migrationBuilder.DropTable(name: "EcoleSignature");
        migrationBuilder.DropTable(name: "EcoleEntete");
        migrationBuilder.DropTable(name: "EcoleLogo");
    }
}
