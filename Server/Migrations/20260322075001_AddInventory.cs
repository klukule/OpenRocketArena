using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerInventories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    RocketBucks = table.Column<int>(type: "INTEGER", nullable: false),
                    RocketParts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerInventories_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBlastPasses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    CmsBlastPassId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBlastPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryBlastPasses_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryChests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    CmsChestId = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryChests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryChests_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDuplicateItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    ItemValue = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDuplicateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDuplicateItems_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    CmsItemId = table.Column<string>(type: "TEXT", nullable: false),
                    ItemCategory = table.Column<string>(type: "TEXT", nullable: false),
                    Viewed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PopUpNotification = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryOneTimeOffers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    OfferId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryOneTimeOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryOneTimeOffers_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryPromotions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InventoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    PromotionId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPromotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryPromotions_PlayerInventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "PlayerInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBlastPasses_InventoryId",
                table: "InventoryBlastPasses",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryChests_InventoryId",
                table: "InventoryChests",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDuplicateItems_InventoryId",
                table: "InventoryDuplicateItems",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId",
                table: "InventoryItems",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOneTimeOffers_InventoryId",
                table: "InventoryOneTimeOffers",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPromotions_InventoryId",
                table: "InventoryPromotions",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerInventories_AccountId",
                table: "PlayerInventories",
                column: "AccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryBlastPasses");

            migrationBuilder.DropTable(
                name: "InventoryChests");

            migrationBuilder.DropTable(
                name: "InventoryDuplicateItems");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "InventoryOneTimeOffers");

            migrationBuilder.DropTable(
                name: "InventoryPromotions");

            migrationBuilder.DropTable(
                name: "PlayerInventories");
        }
    }
}
