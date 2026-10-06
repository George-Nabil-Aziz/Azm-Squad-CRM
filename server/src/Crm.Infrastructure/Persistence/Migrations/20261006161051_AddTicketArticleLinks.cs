using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketArticleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketArticleLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketArticleLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketArticleLinks_AspNetUsers_LinkedById",
                        column: x => x.LinkedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketArticleLinks_KbArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KbArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketArticleLinks_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketArticleLinks_ArticleId",
                table: "TicketArticleLinks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketArticleLinks_LinkedById",
                table: "TicketArticleLinks",
                column: "LinkedById");

            migrationBuilder.CreateIndex(
                name: "IX_TicketArticleLinks_TicketId_LinkedAt",
                table: "TicketArticleLinks",
                columns: new[] { "TicketId", "LinkedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketArticleLinks");
        }
    }
}
