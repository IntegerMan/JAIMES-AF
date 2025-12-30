using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateModelsWithNullEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_Name_Provider_Endpoint",
                table: "Models");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Name_Provider_Endpoint",
                table: "Models",
                columns: new[] { "Name", "Provider", "Endpoint" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Models_Name_Provider_Endpoint",
                table: "Models");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Name_Provider_Endpoint",
                table: "Models",
                columns: new[] { "Name", "Provider", "Endpoint" },
                unique: true);
        }
    }
}
