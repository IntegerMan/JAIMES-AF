using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PlayerId",
                table: "Messages",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Players_PlayerId",
                table: "Messages",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Players_PlayerId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_PlayerId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Messages");
        }
    }
}
