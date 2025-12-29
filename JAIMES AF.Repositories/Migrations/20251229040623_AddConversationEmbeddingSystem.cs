using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationEmbeddingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextMessageId",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousMessageId",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    QdrantPointId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Embedding = table.Column<Vector>(type: "vector", nullable: true),
                    EmbeddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageEmbeddings_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_NextMessageId",
                table: "Messages",
                column: "NextMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PreviousMessageId",
                table: "Messages",
                column: "PreviousMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEmbeddings_MessageId",
                table: "MessageEmbeddings",
                column: "MessageId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_NextMessageId",
                table: "Messages",
                column: "NextMessageId",
                principalTable: "Messages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_PreviousMessageId",
                table: "Messages",
                column: "PreviousMessageId",
                principalTable: "Messages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_NextMessageId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_PreviousMessageId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "MessageEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_Messages_NextMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_PreviousMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "NextMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PreviousMessageId",
                table: "Messages");
        }
    }
}
