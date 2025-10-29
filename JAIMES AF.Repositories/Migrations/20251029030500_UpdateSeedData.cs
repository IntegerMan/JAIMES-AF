using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
 /// <inheritdoc />
 public partial class UpdateSeedData : Migration
 {
 /// <inheritdoc />
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 // Remove old seeded data (if present)
 migrationBuilder.DeleteData(
 table: "Players",
 keyColumn: "Id",
 keyValue: "emcee");

 migrationBuilder.DeleteData(
 table: "Scenarios",
 keyColumn: "Id",
 keyValue: "default");

 migrationBuilder.DeleteData(
 table: "Rulesets",
 keyColumn: "Id",
 keyValue: "DND5E");

 // Insert new seeded data
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
 }

 /// <inheritdoc />
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
 values: new object[] { "DND5E", "Dungeons and Dragons5th Edition" });

 migrationBuilder.InsertData(
 table: "Players",
 columns: new[] { "Id", "Description", "RulesetId" },
 values: new object[] { "emcee", "Default player", "DND5E" });

 migrationBuilder.InsertData(
 table: "Scenarios",
 columns: new[] { "Id", "Description", "RulesetId" },
 values: new object[] { "default", "Default scenario", "DND5E" });
 }
 }
}
