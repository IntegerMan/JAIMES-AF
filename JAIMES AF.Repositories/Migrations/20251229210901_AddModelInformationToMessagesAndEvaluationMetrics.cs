using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddModelInformationToMessagesAndEvaluationMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelEndpoint",
                table: "Messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "Messages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelProvider",
                table: "Messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationModelEndpoint",
                table: "MessageEvaluationMetrics",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationModelName",
                table: "MessageEvaluationMetrics",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationModelProvider",
                table: "MessageEvaluationMetrics",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelEndpoint",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ModelProvider",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "EvaluationModelEndpoint",
                table: "MessageEvaluationMetrics");

            migrationBuilder.DropColumn(
                name: "EvaluationModelName",
                table: "MessageEvaluationMetrics");

            migrationBuilder.DropColumn(
                name: "EvaluationModelProvider",
                table: "MessageEvaluationMetrics");
        }
    }
}
