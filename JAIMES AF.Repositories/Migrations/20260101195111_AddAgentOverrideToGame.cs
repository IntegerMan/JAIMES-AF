using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentOverrideToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstructionVersionId",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_AgentId",
                table: "Games",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_InstructionVersionId",
                table: "Games",
                column: "InstructionVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_AgentInstructionVersions_InstructionVersionId",
                table: "Games",
                column: "InstructionVersionId",
                principalTable: "AgentInstructionVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Agents_AgentId",
                table: "Games",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_AgentInstructionVersions_InstructionVersionId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Agents_AgentId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_AgentId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_InstructionVersionId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "InstructionVersionId",
                table: "Games");
        }
    }
}
