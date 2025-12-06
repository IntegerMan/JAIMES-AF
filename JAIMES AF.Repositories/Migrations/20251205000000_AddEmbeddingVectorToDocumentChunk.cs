using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingVectorToDocumentChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgvector extension if not already enabled
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // Add the Embedding column to DocumentChunks table
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "DocumentChunks",
                type: "vector",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the Embedding column from DocumentChunks table
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "DocumentChunks");
        }
    }
}
