using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNewGameInstructionsFromScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewGameInstructions",
                table: "Scenarios");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "InitialGreeting",
                value: "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewGameInstructions",
                table: "Scenarios",
                type: "text",
                nullable: false,
                defaultValue: "UPDATE ME");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                columns: new[] { "InitialGreeting", "NewGameInstructions" },
                values: new object[] { "We're about to get started on a solo adventure, but before we do, tell me a bit about your character and the style of adventure you'd like to have", "You find yourself washed ashore on a pristine tropical beach. The warm sun beats down on your skin as you take in your surroundings. Crystal-clear turquoise water laps gently at the white sand beach. Behind you, a dense jungle stretches inland, filled with the sounds of exotic birds and rustling leaves. Your gear is scattered nearby, having survived the shipwreck that brought you here. You have no memory of how you arrived, but you know one thing: you must survive and discover the secrets of this mysterious island. What do you do first?" });
        }
    }
}
