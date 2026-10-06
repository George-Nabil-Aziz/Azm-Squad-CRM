using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId_Type_Value",
                table: "CustomerContacts",
                columns: new[] { "CustomerId", "Type", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_Type_Value",
                table: "CustomerContacts",
                columns: new[] { "Type", "Value" });

            // CRM-9 data: every existing email / phone (CRM-8 columns) becomes the customer's primary contact of that
            // type. Emails are trimmed and lower-cased. Phones are normalized as far as SQL can: spaces, dashes, dots and
            // brackets removed; "00…" → "+…"; "0…" → "+966…" (Saudi); "966…" → "+966…"; anything else stays as entered.
            migrationBuilder.Sql(
                """
                UPDATE Customers
                SET Email = NULLIF(LOWER(LTRIM(RTRIM(Email))), ''),
                    Phone = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(Phone)), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '');
                """);
            migrationBuilder.Sql("UPDATE Customers SET Phone = '+' + SUBSTRING(Phone, 3, 32) WHERE Phone LIKE '00%';");
            migrationBuilder.Sql("UPDATE Customers SET Phone = '+966' + SUBSTRING(Phone, 2, 32) WHERE Phone LIKE '0%' AND LEN(Phone) <= 29;");
            migrationBuilder.Sql("UPDATE Customers SET Phone = '+' + Phone WHERE Phone LIKE '966%' AND LEN(Phone) <= 31;");
            migrationBuilder.Sql(
                """
                INSERT INTO CustomerContacts (Id, CustomerId, Type, Value, IsPrimary, CreatedAt)
                SELECT NEWID(), Id, 'Email', Email, CAST(1 AS bit), CreatedAt FROM Customers WHERE Email IS NOT NULL;
                INSERT INTO CustomerContacts (Id, CustomerId, Type, Value, IsPrimary, CreatedAt)
                SELECT NEWID(), Id, 'Phone', Phone, CAST(1 AS bit), CreatedAt FROM Customers WHERE Phone IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerContacts");
        }
    }
}
