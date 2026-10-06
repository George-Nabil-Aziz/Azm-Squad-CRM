using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKbFaqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KbFaqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnswerEn = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    QuestionAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnswerAr = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbFaqs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KbFaqs_IsPublished_DisplayOrder",
                table: "KbFaqs",
                columns: new[] { "IsPublished", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KbFaqs");
        }
    }
}
