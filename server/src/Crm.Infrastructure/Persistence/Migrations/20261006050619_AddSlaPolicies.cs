using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Priority);
                });

            migrationBuilder.InsertData(
                table: "SlaPolicies",
                columns: new[] { "Priority", "ResolutionMinutes", "ResponseMinutes", "UpdatedAt" },
                values: new object[,]
                {
                    { "High", 480, 120, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Low", 4320, 480, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Mid", 1440, 240, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaPolicies");
        }
    }
}
