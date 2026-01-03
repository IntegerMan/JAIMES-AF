using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class MakeLocationNameLowerStored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NameLower",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "LOWER(\"Name\")",
                stored: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComputedColumnSql: "LOWER(\"Name\")",
                oldStored: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NameLower",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "LOWER(\"Name\")",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComputedColumnSql: "LOWER(\"Name\")");
        }
    }
}
