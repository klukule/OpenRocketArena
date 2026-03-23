using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SteamId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthCode = table.Column<string>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsConsumed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthSessions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    NamespaceName = table.Column<string>(type: "TEXT", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personas_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    CareerLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CareerXp = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtifactUnlockLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtifactUnlockProgress = table.Column<float>(type: "REAL", nullable: false),
                    ArtifactUnlockXp = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<float>(type: "REAL", nullable: false),
                    LastPlayedMatchId = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveMatchId = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveMatchIdUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OnboardingState = table.Column<int>(type: "INTEGER", nullable: false),
                    AdvertState = table.Column<bool>(type: "INTEGER", nullable: false),
                    PromosOwned = table.Column<string>(type: "TEXT", nullable: false),
                    BanLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UnbanTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BanMock = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerProfiles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    SlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestId = table.Column<string>(type: "TEXT", nullable: false),
                    Goal = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DismissCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    LastDismissTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedRewardId = table.Column<string>(type: "TEXT", nullable: false),
                    BlastPassXpReward = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quests_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlastPassProgressions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    BlastPassId = table.Column<string>(type: "TEXT", nullable: false),
                    BlastPassXp = table.Column<int>(type: "INTEGER", nullable: false),
                    BlastPassLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    BpProgress = table.Column<float>(type: "REAL", nullable: false),
                    XpBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    PartyXpBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    Viewed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlastPassProgressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlastPassProgressions_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterProgressions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Experience = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<float>(type: "REAL", nullable: false),
                    LastPlayedMatchId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterProgressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterProgressions_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemLevels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalXp = table.Column<float>(type: "REAL", nullable: false),
                    Progress = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemLevels_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MotdViews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    MotdId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotdViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MotdViews_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistRankings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlaylistId = table.Column<string>(type: "TEXT", nullable: false),
                    SkillMean = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillStdDev = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesWon = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesQuit = table.Column<int>(type: "INTEGER", nullable: false),
                    Streak = table.Column<int>(type: "INTEGER", nullable: false),
                    BotLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistRankings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistRankings_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileEquipItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileEquipItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileEquipItems_PlayerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterEmotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterProgressionId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    EmoteType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterEmotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterEmotes_CharacterProgressions_CharacterProgressionId",
                        column: x => x.CharacterProgressionId,
                        principalTable: "CharacterProgressions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterProgressionId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsRanked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSets_CharacterProgressions_CharacterProgressionId",
                        column: x => x.CharacterProgressionId,
                        principalTable: "CharacterProgressions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSetItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipmentSetId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSetItems_EquipmentSets_EquipmentSetId",
                        column: x => x.EquipmentSetId,
                        principalTable: "EquipmentSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SteamId",
                table: "Accounts",
                column: "SteamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlastPassProgressions_ProfileId",
                table: "BlastPassProgressions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEmotes_CharacterProgressionId",
                table: "CharacterEmotes",
                column: "CharacterProgressionId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterProgressions_ProfileId",
                table: "CharacterProgressions",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSetItems_EquipmentSetId",
                table: "EquipmentSetItems",
                column: "EquipmentSetId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSets_CharacterProgressionId",
                table: "EquipmentSets",
                column: "CharacterProgressionId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemLevels_ProfileId",
                table: "ItemLevels",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MotdViews_ProfileId",
                table: "MotdViews",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthSessions_AccountId",
                table: "OAuthSessions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_AccountId",
                table: "Personas",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_AccountId",
                table: "PlayerProfiles",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistRankings_ProfileId",
                table: "PlaylistRankings",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileEquipItems_ProfileId",
                table: "ProfileEquipItems",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_AccountId",
                table: "Quests",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlastPassProgressions");

            migrationBuilder.DropTable(
                name: "CharacterEmotes");

            migrationBuilder.DropTable(
                name: "EquipmentSetItems");

            migrationBuilder.DropTable(
                name: "ItemLevels");

            migrationBuilder.DropTable(
                name: "MotdViews");

            migrationBuilder.DropTable(
                name: "OAuthSessions");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropTable(
                name: "PlaylistRankings");

            migrationBuilder.DropTable(
                name: "ProfileEquipItems");

            migrationBuilder.DropTable(
                name: "Quests");

            migrationBuilder.DropTable(
                name: "EquipmentSets");

            migrationBuilder.DropTable(
                name: "CharacterProgressions");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
