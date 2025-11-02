using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class addingnames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Scenarios",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Players",
                keyColumn: "Id",
                keyValue: "emcee",
                column: "Name",
                value: "Emcee");

            migrationBuilder.UpdateData(
                table: "Rulesets",
                keyColumn: "Id",
                keyValue: "dnd5e",
                column: "Name",
                value: "Dungeons and Dragons5th Edition");

            migrationBuilder.UpdateData(
                table: "Scenarios",
                keyColumn: "Id",
                keyValue: "islandTest",
                column: "Name",
                value: "Island Test");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Players");

            migrationBuilder.UpdateData(
                table: "Rulesets",
                keyColumn: "Id",
                keyValue: "dnd5e",
                column: "Name",
                value: "Dungeons and Dragons 5th Edition");
        }
    }
}
