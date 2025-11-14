using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioSystemPromptAndNewGameInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewGameInstructions",
                table: "Scenarios",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "UPDATE ME");

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "Scenarios",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "UPDATE ME");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                columns: new[] { "NewGameInstructions", "SystemPrompt" },
                values: new object[] { "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?", "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewGameInstructions",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "Scenarios");
        }
    }
}
