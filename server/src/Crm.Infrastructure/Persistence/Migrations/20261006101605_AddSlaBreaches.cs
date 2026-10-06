using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaBreaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ResolutionBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ResponseBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TicketSlaEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSlaEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSlaEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResponseBreached_ResolutionBreached",
                table: "Tickets",
                columns: new[] { "ResponseBreached", "ResolutionBreached" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaEvents_TicketId_Type_Level",
                table: "TicketSlaEvents",
                columns: new[] { "TicketId", "Type", "Level" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSlaEvents");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResponseBreached_ResolutionBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseBreached",
                table: "Tickets");
        }
    }
}
