using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNotesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAttachments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAttachments_CustomerId_UploadedAt",
                table: "CustomerAttachments",
                columns: new[] { "CustomerId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_CustomerId_CreatedAt",
                table: "CustomerNotes",
                columns: new[] { "CustomerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAttachments");

            migrationBuilder.DropTable(
                name: "CustomerNotes");
        }
    }
}
