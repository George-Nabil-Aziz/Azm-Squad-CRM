using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErpIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErpCustomerId",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ErpSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpSyncLogs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ErpCustomerId",
                table: "Customers",
                column: "ErpCustomerId",
                unique: true,
                filter: "[ErpCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErpSyncLogs_CreatedAt",
                table: "ErpSyncLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ErpSyncLogs_CustomerId",
                table: "ErpSyncLogs",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpSyncLogs");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ErpCustomerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ErpCustomerId",
                table: "Customers");
        }
    }
}
