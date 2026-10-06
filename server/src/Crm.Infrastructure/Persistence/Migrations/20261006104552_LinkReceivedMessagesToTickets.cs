using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkReceivedMessagesToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TicketId",
                table: "ReceivedMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedMessages_TicketId",
                table: "ReceivedMessages",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivedMessages_Tickets_TicketId",
                table: "ReceivedMessages",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceivedMessages_Tickets_TicketId",
                table: "ReceivedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ReceivedMessages_TicketId",
                table: "ReceivedMessages");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "ReceivedMessages");
        }
    }
}
