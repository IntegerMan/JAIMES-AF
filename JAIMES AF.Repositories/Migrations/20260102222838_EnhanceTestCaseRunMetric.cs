using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceTestCaseRunMetric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "TestCaseRunMetrics",
                type: "text",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Diagnostics",
                table: "TestCaseRunMetrics",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvaluatedAt",
                table: "TestCaseRunMetrics",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "EvaluationModelId",
                table: "TestCaseRunMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRunMetrics_EvaluationModelId",
                table: "TestCaseRunMetrics",
                column: "EvaluationModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCaseRunMetrics_Models_EvaluationModelId",
                table: "TestCaseRunMetrics",
                column: "EvaluationModelId",
                principalTable: "Models",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestCaseRunMetrics_Models_EvaluationModelId",
                table: "TestCaseRunMetrics");

            migrationBuilder.DropIndex(
                name: "IX_TestCaseRunMetrics_EvaluationModelId",
                table: "TestCaseRunMetrics");

            migrationBuilder.DropColumn(
                name: "Diagnostics",
                table: "TestCaseRunMetrics");

            migrationBuilder.DropColumn(
                name: "EvaluatedAt",
                table: "TestCaseRunMetrics");

            migrationBuilder.DropColumn(
                name: "EvaluationModelId",
                table: "TestCaseRunMetrics");

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "TestCaseRunMetrics",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
