using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalationLevel",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ResolutionBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResponseBreached",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseWarnedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseWarningAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.InsertData(
                table: "SlaPolicies",
                columns: new[] { "Priority", "ResolutionMinutes", "ResponseMinutes", "UpdatedAt" },
                values: new object[,]
                {
                    { "High", 480, 120, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Low", 4320, 480, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Mid", 1440, 240, new DateTime(2026, 10, 6, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResolutionDueAt",
                table: "Tickets",
                column: "ResolutionDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResponseBreached_ResolutionBreached",
                table: "Tickets",
                columns: new[] { "ResponseBreached", "ResolutionBreached" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResponseDueAt",
                table: "Tickets",
                column: "ResponseDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResponseWarningAt",
                table: "Tickets",
                column: "ResponseWarningAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_ReadAt",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TicketId",
                table: "Notifications",
                column: "TicketId");

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
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "TicketSlaEvents");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResolutionDueAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResponseBreached_ResolutionBreached",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResponseDueAt",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResponseWarningAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalationLevel",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseBreached",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseWarnedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseWarningAt",
                table: "Tickets");
        }
    }
}
