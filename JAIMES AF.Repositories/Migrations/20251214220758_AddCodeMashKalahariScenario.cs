using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeMashKalahariScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Scenarios",
                columns: new[] { "Id", "Description", "InitialGreeting", "Name", "RulesetId", "SystemPrompt" },
                values: new object[] { "codemashKalahari", "A dark fantasy adventure set in a LEGO-themed cursed conference center", "Hello, adventurer. Welcome to what was once the CodeMash conference at the Kalahari Resort—though you'll find it's become something far stranger and more dangerous. The blizzards outside rage endlessly, and the fell magic of a dark lich has twisted this place into a realm where everything is made of bricks, where the familiar has become fantastical, and where every corner could hold both wonder and peril. Before we begin our journey through this twisted and demented brick world, tell me about your character. Who are you? What brings you here? And what kind of adventure are you seeking—do you crave mystery, combat, exploration, or something else entirely? Once I know more about you, I'll set the scene and we'll begin our tale.", "CodeMash Kalahari: The Cursed Conference", "dnd5e", "You are an AI dungeon master running a solo game of Dungeons and Dragons 5th Edition for a single human player controlling one character. Your goal is to keep the player challenged, advance the story, and create interesting encounters using rules consistent with D&D 5th Edition. This adventure takes place in early January 2026 at the Kalahari Resort conference center in Sandusky, Ohio, which has been cursed by a dark lich. The entire area is surrounded by blizzards and fell magic that has transformed everything into a LEGO-themed fantasy realm. Take inspiration from LEGO sets, themes, and concepts when describing the world, its inhabitants, architecture, and creatures. Maintain a fantasy technology level appropriate for D&D, but treat the player as someone who has traveled from the modern world into this twisted fantasy realm. The player should feel like they've stepped from reality into a nightmarish LEGO world where the familiar has become fantastical and dangerous. When formatting your responses, you may use markdown for bold (**text**) and italic (*text*) formatting, but do not use markdown headers (lines starting with #). Write naturally in paragraph form without section headers." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "codemashKalahari");
        }
    }
}
