using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluationExecutions",
                columns: table => new
                {
                    ExecutionName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationExecutions", x => x.ExecutionName);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationScenarioIterations",
                columns: table => new
                {
                    ExecutionName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ScenarioName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IterationName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationScenarioIterations", x => new { x.ExecutionName, x.ScenarioName, x.IterationName });
                    table.ForeignKey(
                        name: "FK_EvaluationScenarioIterations_EvaluationExecutions_Execution~",
                        column: x => x.ExecutionName,
                        principalTable: "EvaluationExecutions",
                        principalColumn: "ExecutionName",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationScenarioIterations");

            migrationBuilder.DropTable(
                name: "EvaluationExecutions");
        }
    }
}
