using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialGreetingToScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitialGreeting",
                table: "Scenarios",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "InitialGreeting",
                value: "We're about to get started on a solo adventure, but before we do, tell me a bit about your character and the style of adventure you'd like to have");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialGreeting",
                table: "Scenarios");
        }
    }
}
