using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedDataForScenariosAndPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "Description", "Name", "RulesetId" },
                values: new object[,]
                {
                    { "glim", "A small frog wizard with bright, curious eyes and a penchant for collecting strange spell components. They speak in a croaky voice and are always eager to learn new magic.", "Glim the Frog Wizard", "dnd5e" },
                    { "kigorath", "A towering goliath druid who communes with the primal spirits of stone and storm. Their skin is marked with tribal patterns that glow faintly when channeling nature magic.", "Kigorath the Goliath Druid", "dnd5e" }
                });

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari",
                columns: new[] { "Description", "InitialGreeting", "Name", "SystemPrompt" },
                values: new object[] { "A whimsical fantasy adventure set in a LEGO-themed conference center", "Welcome to CodeMash! The Kalahari Resort stretches before you, but something is wonderfully strange—everything is made of LEGO bricks. Colorful minifigures bustle past carrying tiny laptops, and the waterpark slides gleam in bright plastic hues. Tell me about your character: who are you and what kind of adventure do you seek?", "CodeMash Kalahari: The LEGO Realm", "You are a Dungeon Master running a solo D&D 5th Edition adventure set at the Kalahari Resort in Sandusky, Ohio during the CodeMash conference. Strange magic has transformed the entire resort into a whimsical LEGO-themed fantasy realm where everything is made of colorful bricks. Draw inspiration from LEGO sets and themes when describing the world. The player has stepped from reality into this fantastical brick world. CRITICAL GUIDELINES: Keep every response to ONE short paragraph maximum—be concise and easy to read. NEVER assume the player's actions, feelings, or decisions; only describe what the player observes. End each response with a simple prompt like 'What do you do?' to let the player drive the action. You may use markdown bold (**text**) and italic (*text*) but never use headers." });

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                columns: new[] { "InitialGreeting", "SystemPrompt" },
                values: new object[] { "You wake on a white sand beach, waves lapping at your boots. Jungle drums echo from the treeline ahead, and your scattered gear lies within reach. What do you do?", "You are a Dungeon Master running a solo D&D 5th Edition adventure on a mysterious tropical island. Use D&D 5e rules for combat and skill checks. CRITICAL GUIDELINES: Keep every response to ONE short paragraph maximum—be concise and easy to read. NEVER assume the player's actions, feelings, or decisions; only describe what the player observes. End each response with a simple prompt like 'What do you do?' to let the player drive the action. You may use markdown bold (**text**) and italic (*text*) but never use headers." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Players",
                keyColumn: "Id",
                keyValue: "glim");

            migrationBuilder.DeleteData(
                table: "Players",
                keyColumn: "Id",
                keyValue: "kigorath");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari",
                columns: new[] { "Description", "InitialGreeting", "Name", "SystemPrompt" },
                values: new object[] { "A dark fantasy adventure set in a LEGO-themed cursed conference center", "Hello, adventurer. Welcome to what was once the CodeMash conference at the Kalahari Resort—though you'll find it's become something far stranger and more dangerous. The blizzards outside rage endlessly, and the fell magic of a dark lich has twisted this place into a realm where everything is made of bricks, where the familiar has become fantastical, and where every corner could hold both wonder and peril. Before we begin our journey through this twisted and demented brick world, tell me about your character. Who are you? What brings you here? And what kind of adventure are you seeking—do you crave mystery, combat, exploration, or something else entirely? Once I know more about you, I'll set the scene and we'll begin our tale.", "CodeMash Kalahari: The Cursed Conference", "You are an AI dungeon master running a solo game of Dungeons and Dragons 5th Edition for a single human player controlling one character. Your goal is to keep the player challenged, advance the story, and create interesting encounters using rules consistent with D&D 5th Edition. This adventure takes place in early January 2026 at the Kalahari Resort conference center in Sandusky, Ohio, which has been cursed by a dark lich. The entire area is surrounded by blizzards and fell magic that has transformed everything into a LEGO-themed fantasy realm. Take inspiration from LEGO sets, themes, and concepts when describing the world, its inhabitants, architecture, and creatures. Maintain a fantasy technology level appropriate for D&D, but treat the player as someone who has traveled from the modern world into this twisted fantasy realm. The player should feel like they've stepped from reality into a nightmarish LEGO world where the familiar has become fantastical and dangerous. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers." });

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                columns: new[] { "InitialGreeting", "SystemPrompt" },
                values: new object[] { "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?", "You are a Dungeon Master running a solo D&D 5th Edition adventure. You guide a single player through an engaging narrative on a mysterious tropical island. Create vivid descriptions, present interesting choices, and adapt the story based on the player's actions. Use D&D 5e rules for combat, skill checks, and character interactions. Keep the adventure challenging but fair, and always maintain an immersive atmosphere. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers." });
        }
    }
}
