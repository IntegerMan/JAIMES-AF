using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSystemPromptFromScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "Scenarios");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari",
                column: "ScenarioInstructions",
                value: "The entire Kalahari Resort has been magically transformed into a LEGO-themed fantasy realm. Everything is made of colorful bricks - buildings, furniture, even the people are minifigures. Conference attendees have become adventurers on quests involving programming puzzles, LEGO building challenges, and whimsical brick-based combat. The tone is lighthearted and creative, with opportunities for clever problem-solving and LEGO-inspired humor. Players can build, rebuild, and explore this vibrant, colorful world.");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "ScenarioInstructions",
                value: "This tropical island scenario features mysterious jungles, ancient ruins, and hidden dangers. The atmosphere is one of discovery and survival - players encounter exotic wildlife, tribal inhabitants, and forgotten treasures. Maintain a sense of wonder and exploration while keeping the tone adventurous and slightly perilous. The island has a rich history of shipwrecks, pirate legends, and ancient civilizations.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "Scenarios",
                type: "text",
                nullable: false,
                defaultValue: "UPDATE ME");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari",
                columns: new[] { "ScenarioInstructions", "SystemPrompt" },
                values: new object[] { null, "You are a Dungeon Master running a solo D&D 5th Edition adventure set at the Kalahari Resort in Sandusky, Ohio during the CodeMash conference. Strange magic has transformed the entire resort into a whimsical LEGO-themed fantasy realm where everything is made of colorful bricks. Draw inspiration from LEGO sets and themes when describing the world. The player has stepped from reality into this fantastical brick world. CRITICAL GUIDELINES: Keep every response to ONE short paragraph maximum—be concise and easy to read. NEVER assume the player's actions, feelings, or decisions; only describe what the player observes. End each response with a simple prompt like 'What do you do?' to let the player drive the action. You may use markdown bold (**text**) and italic (*text*) but never use headers." });

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                columns: new[] { "ScenarioInstructions", "SystemPrompt" },
                values: new object[] { null, "You are a Dungeon Master running a solo D&D 5th Edition adventure on a mysterious tropical island. Use D&D 5e rules for combat and skill checks. CRITICAL GUIDELINES: Keep every response to ONE short paragraph maximum—be concise and easy to read. NEVER assume the player's actions, feelings, or decisions; only describe what the player observes. End each response with a simple prompt like 'What do you do?' to let the player drive the action. You may use markdown bold (**text**) and italic (*text*) but never use headers." });
        }
    }
}
