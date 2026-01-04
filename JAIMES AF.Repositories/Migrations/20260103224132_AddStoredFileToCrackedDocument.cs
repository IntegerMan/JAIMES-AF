using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFileToCrackedDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoredFileId",
                table: "CrackedDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrackedDocuments_StoredFileId",
                table: "CrackedDocuments",
                column: "StoredFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_CrackedDocuments_StoredFiles_StoredFileId",
                table: "CrackedDocuments",
                column: "StoredFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrackedDocuments_StoredFiles_StoredFileId",
                table: "CrackedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_CrackedDocuments_StoredFileId",
                table: "CrackedDocuments");

            migrationBuilder.DropColumn(
                name: "StoredFileId",
                table: "CrackedDocuments");
        }
    }
}
