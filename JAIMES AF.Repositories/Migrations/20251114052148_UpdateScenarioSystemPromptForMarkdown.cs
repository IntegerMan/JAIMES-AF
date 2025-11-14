using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class UpdateScenarioSystemPromptForMarkdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "SystemPrompt",
                value: "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "SystemPrompt",
                value: "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere.");
        }
    }
}
