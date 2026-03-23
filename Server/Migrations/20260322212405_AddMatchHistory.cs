using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    MatchEndJsonData = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchHistories_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_AccountId",
                table: "MatchHistories",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchHistories");
        }
    }
}
