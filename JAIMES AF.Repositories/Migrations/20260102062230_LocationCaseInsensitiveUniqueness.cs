using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class LocationCaseInsensitiveUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_GameId_Name",
                table: "Locations");

            migrationBuilder.AddColumn<string>(
                name: "NameLower",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "LOWER(\"Name\")");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_GameId_NameLower",
                table: "Locations",
                columns: new[] { "GameId", "NameLower" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_GameId_NameLower",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "NameLower",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_GameId_Name",
                table: "Locations",
                columns: new[] { "GameId", "Name" },
                unique: true);
        }
    }
}
