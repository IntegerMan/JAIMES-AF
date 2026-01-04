using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddClassifierTrainingJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ClassificationModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TrainingJobId",
                table: "ClassificationModels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassificationModelTrainingJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MinConfidence = table.Column<double>(type: "double precision", nullable: false),
                    TrainTestSplit = table.Column<double>(type: "double precision", nullable: false),
                    TrainingTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    OptimizingMetric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: true),
                    TrainingRows = table.Column<int>(type: "integer", nullable: true),
                    TestRows = table.Column<int>(type: "integer", nullable: true),
                    MacroAccuracy = table.Column<double>(type: "double precision", nullable: true),
                    MicroAccuracy = table.Column<double>(type: "double precision", nullable: true),
                    MacroPrecision = table.Column<double>(type: "double precision", nullable: true),
                    MacroRecall = table.Column<double>(type: "double precision", nullable: true),
                    LogLoss = table.Column<double>(type: "double precision", nullable: true),
                    ConfusionMatrixJson = table.Column<string>(type: "text", nullable: true),
                    TrainerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClassificationModelId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationModelTrainingJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationModels_ModelType_IsActive",
                table: "ClassificationModels",
                columns: new[] { "ModelType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationModels_TrainingJobId",
                table: "ClassificationModels",
                column: "TrainingJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationModelTrainingJobs_Status",
                table: "ClassificationModelTrainingJobs",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassificationModels_ClassificationModelTrainingJobs_Traini~",
                table: "ClassificationModels",
                column: "TrainingJobId",
                principalTable: "ClassificationModelTrainingJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassificationModels_ClassificationModelTrainingJobs_Traini~",
                table: "ClassificationModels");

            migrationBuilder.DropTable(
                name: "ClassificationModelTrainingJobs");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationModels_ModelType_IsActive",
                table: "ClassificationModels");

            migrationBuilder.DropIndex(
                name: "IX_ClassificationModels_TrainingJobId",
                table: "ClassificationModels");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ClassificationModels");

            migrationBuilder.DropColumn(
                name: "TrainingJobId",
                table: "ClassificationModels");
        }
    }
}
