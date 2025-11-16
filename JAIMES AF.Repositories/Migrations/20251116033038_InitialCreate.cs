using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rulesets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rulesets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RulesetId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Rulesets_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Rulesets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RulesetId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "UPDATE ME"),
                    NewGameInstructions = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "UPDATE ME")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_Rulesets_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Rulesets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousHistoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatHistories_ChatHistories_PreviousHistoryId",
                        column: x => x.PreviousHistoryId,
                        principalTable: "ChatHistories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RulesetId = table.Column<string>(type: "TEXT", nullable: false),
                    ScenarioId = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MostRecentHistoryId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_ChatHistories_MostRecentHistoryId",
                        column: x => x.MostRecentHistoryId,
                        principalTable: "ChatHistories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Games_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Rulesets_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Rulesets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: true),
                    ChatHistoryId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_ChatHistories_ChatHistoryId",
                        column: x => x.ChatHistoryId,
                        principalTable: "ChatHistories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Rulesets",
                columns: new[] { "Id", "Name" },
                values: new object[] { "dnd5e", "Dungeons and Dragons 5th Edition" });

            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "Description", "Name", "RulesetId" },
                values: new object[] { "emcee", "Default player", "Emcee", "dnd5e" });

            migrationBuilder.InsertData(
                table: "Scenarios",
                columns: new[] { "Id", "Description", "Name", "NewGameInstructions", "RulesetId", "SystemPrompt" },
                values: new object[] { "islandTest", "Island test scenario", "Island Test", "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?", "dnd5e", "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers." });

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_GameId",
                table: "ChatHistories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_MessageId",
                table: "ChatHistories",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_PreviousHistoryId",
                table: "ChatHistories",
                column: "PreviousHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MostRecentHistoryId",
                table: "Games",
                column: "MostRecentHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_PlayerId",
                table: "Games",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_RulesetId",
                table: "Games",
                column: "RulesetId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ScenarioId",
                table: "Games",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatHistoryId",
                table: "Messages",
                column: "ChatHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GameId",
                table: "Messages",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PlayerId",
                table: "Messages",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_RulesetId",
                table: "Players",
                column: "RulesetId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_RulesetId",
                table: "Scenarios",
                column: "RulesetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatHistories_Games_GameId",
                table: "ChatHistories",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatHistories_Messages_MessageId",
                table: "ChatHistories",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatHistories_Games_GameId",
                table: "ChatHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Games_GameId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatHistories_Messages_MessageId",
                table: "ChatHistories");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Scenarios");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ChatHistories");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Rulesets");
        }
    }
}
