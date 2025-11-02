using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
 public partial class AddNameToPlayerAndScenario : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 // Add columns with default value for existing rows
 migrationBuilder.AddColumn<string>(
 name: "Name",
 table: "Players",
 type: "nvarchar(max)",
 nullable: false,
 defaultValue: "Unspecified");

 migrationBuilder.AddColumn<string>(
 name: "Name",
 table: "Scenarios",
 type: "nvarchar(max)",
 nullable: false,
 defaultValue: "Unspecified");

 // Update seeded data to include Name value
 migrationBuilder.DeleteData(
 table: "Players",
 keyColumn: "Id",
 keyValue: "emcee");

 migrationBuilder.DeleteData(
 table: "Scenarios",
 keyColumn: "Id",
 keyValue: "islandTest");

 migrationBuilder.DeleteData(
 table: "Rulesets",
 keyColumn: "Id",
 keyValue: "dnd5e");

 migrationBuilder.InsertData(
 table: "Rulesets",
 columns: new[] { "Id", "Name" },
 values: new object[] { "dnd5e", "Dungeons and Dragons5th Edition" });

 migrationBuilder.InsertData(
 table: "Players",
 columns: new[] { "Id", "Description", "RulesetId", "Name" },
 values: new object[] { "emcee", "Default player", "dnd5e", "Unspecified" });

 migrationBuilder.InsertData(
 table: "Scenarios",
 columns: new[] { "Id", "Description", "RulesetId", "Name" },
 values: new object[] { "islandTest", "Island test scenario", "dnd5e", "Unspecified" });
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DeleteData(
 table: "Players",
 keyColumn: "Id",
 keyValue: "emcee");

 migrationBuilder.DeleteData(
 table: "Scenarios",
 keyColumn: "Id",
 keyValue: "islandTest");

 migrationBuilder.DeleteData(
 table: "Rulesets",
 keyColumn: "Id",
 keyValue: "dnd5e");

 migrationBuilder.InsertData(
 table: "Rulesets",
 columns: new[] { "Id", "Name" },
 values: new object[] { "dnd5e", "Dungeons and Dragons5th Edition" });

 migrationBuilder.InsertData(
 table: "Players",
 columns: new[] { "Id", "Description", "RulesetId" },
 values: new object[] { "emcee", "Default player", "dnd5e" });

 migrationBuilder.InsertData(
 table: "Scenarios",
 columns: new[] { "Id", "Description", "RulesetId" },
 values: new object[] { "islandTest", "Island test scenario", "dnd5e" });

 migrationBuilder.DropColumn(
 name: "Name",
 table: "Players");

 migrationBuilder.DropColumn(
 name: "Name",
 table: "Scenarios");
 }
 }
}
