using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RagSearchQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: true),
                    IndexName = table.Column<string>(type: "text", nullable: false),
                    FilterJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagSearchQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RagSearchResultChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RagSearchQueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<string>(type: "text", nullable: false),
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    EmbeddingId = table.Column<string>(type: "text", nullable: false),
                    RulesetId = table.Column<string>(type: "text", nullable: false),
                    Relevancy = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RagSearchResultChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RagSearchResultChunks_RagSearchQueries_RagSearchQueryId",
                        column: x => x.RagSearchQueryId,
                        principalTable: "RagSearchQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchQueries_CreatedAt",
                table: "RagSearchQueries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchQueries_RulesetId",
                table: "RagSearchQueries",
                column: "RulesetId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_ChunkId",
                table: "RagSearchResultChunks",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_RagSearchQueryId",
                table: "RagSearchResultChunks",
                column: "RagSearchQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_RagSearchResultChunks_Relevancy",
                table: "RagSearchResultChunks",
                column: "Relevancy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RagSearchResultChunks");

            migrationBuilder.DropTable(
                name: "RagSearchQueries");
        }
    }
}
