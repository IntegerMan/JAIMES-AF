using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddChatHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatHistoryId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MostRecentHistoryId",
                table: "Games",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatHistories_ChatHistories_PreviousHistoryId",
                        column: x => x.PreviousHistoryId,
                        principalTable: "ChatHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ChatHistories_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatHistories_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatHistoryId",
                table: "Messages",
                column: "ChatHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MostRecentHistoryId",
                table: "Games",
                column: "MostRecentHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_GameId",
                table: "ChatHistories",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_MessageId",
                table: "ChatHistories",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_PreviousHistoryId",
                table: "ChatHistories",
                column: "PreviousHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_ChatHistories_MostRecentHistoryId",
                table: "Games",
                column: "MostRecentHistoryId",
                principalTable: "ChatHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ChatHistories_ChatHistoryId",
                table: "Messages",
                column: "ChatHistoryId",
                principalTable: "ChatHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_ChatHistories_MostRecentHistoryId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ChatHistories_ChatHistoryId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ChatHistories");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatHistoryId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Games_MostRecentHistoryId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ChatHistoryId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MostRecentHistoryId",
                table: "Games");
        }
    }
}
