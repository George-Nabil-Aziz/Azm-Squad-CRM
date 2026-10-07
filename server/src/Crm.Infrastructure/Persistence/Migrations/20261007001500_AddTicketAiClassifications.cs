using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAiClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAiClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestedPriority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    CategoryApplied = table.Column<bool>(type: "bit", nullable: false),
                    PriorityApplied = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CategoryOverriddenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CategoryOverriddenTo = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriorityOverriddenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriorityOverriddenTo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OverriddenById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAiClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAiClassifications_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiClassifications_TicketId",
                table: "TicketAiClassifications",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAiClassifications");
        }
    }
}
