using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddIsScriptedMessageAndNonNullableAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing NULL values before making columns non-nullable
            migrationBuilder.Sql(@"
                UPDATE ""Messages"" 
                SET ""AgentId"" = 'unknown' 
                WHERE ""AgentId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Messages"" 
                SET ""InstructionVersionId"" = 0 
                WHERE ""InstructionVersionId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "InstructionVersionId",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Messages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsScriptedMessage",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsScriptedMessage",
                table: "Messages");

            migrationBuilder.AlterColumn<int>(
                name: "InstructionVersionId",
                table: "Messages",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
