using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentInstructionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScenarioInstructions",
                table: "Scenarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "Messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstructionVersionId",
                table: "Messages",
                type: "integer",
                nullable: true);

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

            // Create default agent
            migrationBuilder.InsertData(
                table: "Agents",
                columns: new[] { "Id", "Name", "Role" },
                values: new object[] { "defaultGameMaster", "Default Game Master", "GameMaster" });

            // Create base instruction version with common D&D 5e DM instructions
            // This will be the base for all scenarios, with scenario-specific instructions in ScenarioInstructions
            migrationBuilder.InsertData(
                table: "AgentInstructionVersions",
                columns: new[] { "AgentId", "VersionNumber", "Instructions", "CreatedAt", "IsActive" },
                values: new object[] 
                { 
                    "defaultGameMaster", 
                    "1.0.0", 
                    "You are a Dungeon Master running a solo D&D 5th Edition adventure. Use D&D 5e rules for combat and skill checks. CRITICAL GUIDELINES: Keep every response to ONE short paragraph maximum—be concise and easy to read. NEVER assume the player's actions, feelings, or decisions; only describe what the player observes. End each response with a simple prompt like 'What do you do?' to let the player drive the action. You may use markdown bold (**text**) and italic (*text*) but never use headers.",
                    DateTime.UtcNow,
                    true
                });

            // Create ScenarioAgent entries for existing scenarios
            // Use a subquery to get the instruction version ID
            migrationBuilder.Sql(@"
                INSERT INTO ""ScenarioAgents"" (""ScenarioId"", ""AgentId"", ""InstructionVersionId"")
                SELECT s.""Id"", 'defaultGameMaster', (
                    SELECT ""Id"" FROM ""AgentInstructionVersions""
                    WHERE ""AgentId"" = 'defaultGameMaster' AND ""VersionNumber"" = '1.0.0'
                    LIMIT 1
                )
                FROM ""Scenarios"" s
                WHERE s.""Id"" IN ('islandTest', 'codemashKalahari');
            ");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari",
                column: "ScenarioInstructions",
                value: null);

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "ScenarioInstructions",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AgentId",
                table: "Messages",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_InstructionVersionId",
                table: "Messages",
                column: "InstructionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstructionVersions_AgentId_VersionNumber",
                table: "AgentInstructionVersions",
                columns: new[] { "AgentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAgents_AgentId",
                table: "ScenarioAgents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAgents_InstructionVersionId",
                table: "ScenarioAgents",
                column: "InstructionVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AgentInstructionVersions_InstructionVersionId",
                table: "Messages",
                column: "InstructionVersionId",
                principalTable: "AgentInstructionVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Agents_AgentId",
                table: "Messages",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AgentInstructionVersions_InstructionVersionId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Agents_AgentId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ScenarioAgents");

            migrationBuilder.DropTable(
                name: "AgentInstructionVersions");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Messages_AgentId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_InstructionVersionId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ScenarioInstructions",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "InstructionVersionId",
                table: "Messages");
        }
    }
}
