using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class FixAgentInstructionVersionModelRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentInstructionVersions_Models_ModelId1",
                table: "AgentInstructionVersions");

            migrationBuilder.DropIndex(
                name: "IX_AgentInstructionVersions_ModelId1",
                table: "AgentInstructionVersions");

            migrationBuilder.DropColumn(
                name: "ModelId1",
                table: "AgentInstructionVersions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelId1",
                table: "AgentInstructionVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstructionVersions_ModelId1",
                table: "AgentInstructionVersions",
                column: "ModelId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentInstructionVersions_Models_ModelId1",
                table: "AgentInstructionVersions",
                column: "ModelId1",
                principalTable: "Models",
                principalColumn: "Id");
        }
    }
}
