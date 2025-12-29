using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrackedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FilePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RelativeDirectory = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CrackedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    ProcessedChunkCount = table.Column<int>(type: "integer", nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RulesetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrackedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FilePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastScanned = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RulesetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RagSearchQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: true),
                    IndexName = table.Column<string>(type: "text", nullable: false),
                    FilterJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagSearchQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rulesets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rulesets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentInstructionVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    VersionNumber = table.Column<string>(type: "text", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentInstructionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentInstructionVersions_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChunkId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QdrantPointId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Embedding = table.Column<Vector>(type: "vector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_CrackedDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CrackedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RagSearchResultChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RagSearchQueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<string>(type: "text", nullable: false),
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    EmbeddingId = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: false),
                    Relevancy = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagSearchResultChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RagSearchResultChunks_RagSearchQueries_RagSearchQueryId",
                        column: x => x.RagSearchQueryId,
                        principalTable: "RagSearchQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
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
                    Id = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ScenarioInstructions = table.Column<string>(type: "text", nullable: true),
                    InitialGreeting = table.Column<string>(type: "text", nullable: true)
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
                name: "ScenarioAgents",
                columns: table => new
                {
                    ScenarioId = table.Column<string>(type: "text", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    InstructionVersionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioAgents", x => new { x.ScenarioId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_ScenarioAgents_AgentInstructionVersions_InstructionVersionId",
                        column: x => x.InstructionVersionId,
                        principalTable: "AgentInstructionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioAgents_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioAgents_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousHistoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<int>(type: "integer", nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: false),
                    ScenarioId = table.Column<string>(type: "text", nullable: false),
                    PlayerId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MostRecentHistoryId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayerId = table.Column<string>(type: "text", nullable: true),
                    AgentId = table.Column<string>(type: "text", nullable: true),
                    InstructionVersionId = table.Column<int>(type: "integer", nullable: true),
                    ChatHistoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousMessageId = table.Column<int>(type: "integer", nullable: true),
                    NextMessageId = table.Column<int>(type: "integer", nullable: true),
                    Sentiment = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_AgentInstructionVersions_InstructionVersionId",
                        column: x => x.InstructionVersionId,
                        principalTable: "AgentInstructionVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id");
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
                        name: "FK_Messages_Messages_NextMessageId",
                        column: x => x.NextMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Messages_PreviousMessageId",
                        column: x => x.PreviousMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MessageEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    QdrantPointId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Embedding = table.Column<Vector>(type: "vector", nullable: true),
                    EmbeddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageEmbeddings_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageEvaluationMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Diagnostics = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEvaluationMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageEvaluationMetrics_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Rulesets",
                columns: new[] { "Id", "Name" },
                values: new object[] { "dnd5e", "Dungeons and Dragons 5th Edition" });

            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "Description", "Name", "RulesetId" },
                values: new object[,]
                {
                    { "emcee", "Default player", "Emcee", "dnd5e" },
                    { "glim", "A small frog wizard with bright, curious eyes and a penchant for collecting strange spell components. They speak in a croaky voice and are always eager to learn new magic.", "Glim the Frog Wizard", "dnd5e" },
                    { "kigorath", "A towering goliath druid who communes with the primal spirits of stone and storm. Their skin is marked with tribal patterns that glow faintly when channeling nature magic.", "Kigorath the Goliath Druid", "dnd5e" }
                });

            migrationBuilder.InsertData(
                table: "Scenarios",
                columns: new[] { "Id", "Description", "InitialGreeting", "Name", "RulesetId", "ScenarioInstructions" },
                values: new object[,]
                {
                    { "codemashKalahari", "A whimsical fantasy adventure set in a LEGO-themed conference center", "Welcome to CodeMash! The Kalahari Resort stretches before you, but something is wonderfully strange—everything is made of LEGO bricks. Colorful minifigures bustle past carrying tiny laptops, and the waterpark slides gleam in bright plastic hues. Tell me about your character: who are you and what kind of adventure do you seek?", "CodeMash Kalahari: The LEGO Realm", "dnd5e", "The entire Kalahari Resort has been magically transformed into a LEGO-themed fantasy realm. Everything is made of colorful bricks - buildings, furniture, even the people are minifigures. Conference attendees have become adventurers on quests involving programming puzzles, LEGO building challenges, and whimsical brick-based combat. The tone is lighthearted and creative, with opportunities for clever problem-solving and LEGO-inspired humor. Players can build, rebuild, and explore this vibrant, colorful world." },
                    { "islandTest", "Island test scenario", "You wake on a white sand beach, waves lapping at your boots. Jungle drums echo from the treeline ahead, and your scattered gear lies within reach. What do you do?", "Island Test", "dnd5e", "This tropical island scenario features mysterious jungles, ancient ruins, and hidden dangers. The atmosphere is one of discovery and survival - players encounter exotic wildlife, tribal inhabitants, and forgotten treasures. Maintain a sense of wonder and exploration while keeping the tone adventurous and slightly perilous. The island has a rich history of shipwrecks, pirate legends, and ancient civilizations." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstructionVersions_AgentId_VersionNumber",
                table: "AgentInstructionVersions",
                columns: new[] { "AgentId", "VersionNumber" },
                unique: true);

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
                name: "IX_CrackedDocuments_FilePath",
                table: "CrackedDocuments",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_ChunkId",
                table: "DocumentChunks",
                column: "ChunkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMetadata_FilePath",
                table: "DocumentMetadata",
                column: "FilePath",
                unique: true);

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
                name: "IX_MessageEmbeddings_MessageId",
                table: "MessageEmbeddings",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvaluationMetrics_EvaluatedAt",
                table: "MessageEvaluationMetrics",
                column: "EvaluatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvaluationMetrics_MessageId",
                table: "MessageEvaluationMetrics",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvaluationMetrics_MetricName",
                table: "MessageEvaluationMetrics",
                column: "MetricName");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AgentId",
                table: "Messages",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatHistoryId",
                table: "Messages",
                column: "ChatHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GameId",
                table: "Messages",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_InstructionVersionId",
                table: "Messages",
                column: "InstructionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_NextMessageId",
                table: "Messages",
                column: "NextMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PlayerId",
                table: "Messages",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PreviousMessageId",
                table: "Messages",
                column: "PreviousMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_RulesetId",
                table: "Players",
                column: "RulesetId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchQueries_CreatedAt",
                table: "RagSearchQueries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchQueries_RulesetId",
                table: "RagSearchQueries",
                column: "RulesetId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_ChunkId",
                table: "RagSearchResultChunks",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_RagSearchQueryId",
                table: "RagSearchResultChunks",
                column: "RagSearchQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_Relevancy",
                table: "RagSearchResultChunks",
                column: "Relevancy");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAgents_AgentId",
                table: "ScenarioAgents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAgents_InstructionVersionId",
                table: "ScenarioAgents",
                column: "InstructionVersionId");

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
                name: "FK_AgentInstructionVersions_Agents_AgentId",
                table: "AgentInstructionVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Agents_AgentId",
                table: "Messages");

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
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "DocumentMetadata");

            migrationBuilder.DropTable(
                name: "MessageEmbeddings");

            migrationBuilder.DropTable(
                name: "MessageEvaluationMetrics");

            migrationBuilder.DropTable(
                name: "RagSearchResultChunks");

            migrationBuilder.DropTable(
                name: "ScenarioAgents");

            migrationBuilder.DropTable(
                name: "CrackedDocuments");

            migrationBuilder.DropTable(
                name: "RagSearchQueries");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Scenarios");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "AgentInstructionVersions");

            migrationBuilder.DropTable(
                name: "ChatHistories");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Rulesets");
        }
    }
}
