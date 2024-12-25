using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faucet.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TransactionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionHistory_UserHash",
                table: "TransactionHistory",
                column: "UserHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionHistory");
        }
    }
}
