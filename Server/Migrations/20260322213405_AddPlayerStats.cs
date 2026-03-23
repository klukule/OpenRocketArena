using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerStatGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    SliceType = table.Column<string>(type: "TEXT", nullable: false),
                    SliceValue = table.Column<string>(type: "TEXT", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWon = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesQuit = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesDrawn = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStatGroups_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerStatEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatGroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    Metric = table.Column<string>(type: "TEXT", nullable: false),
                    Min = table.Column<float>(type: "REAL", nullable: false),
                    Max = table.Column<float>(type: "REAL", nullable: false),
                    Sum = table.Column<float>(type: "REAL", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStatEntries_PlayerStatGroups_StatGroupId",
                        column: x => x.StatGroupId,
                        principalTable: "PlayerStatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatEntries_StatGroupId",
                table: "PlayerStatEntries",
                column: "StatGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatGroups_ProfileId_SliceType_SliceValue",
                table: "PlayerStatGroups",
                columns: new[] { "ProfileId", "SliceType", "SliceValue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerStatEntries");

            migrationBuilder.DropTable(
                name: "PlayerStatGroups");
        }
    }
}
