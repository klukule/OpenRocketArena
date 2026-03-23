using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStatIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerStatEntries_StatGroupId",
                table: "PlayerStatEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatEntries_StatGroupId_Metric",
                table: "PlayerStatEntries",
                columns: new[] { "StatGroupId", "Metric" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerStatEntries_StatGroupId_Metric",
                table: "PlayerStatEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatEntries_StatGroupId",
                table: "PlayerStatEntries",
                column: "StatGroupId");
        }
    }
}
