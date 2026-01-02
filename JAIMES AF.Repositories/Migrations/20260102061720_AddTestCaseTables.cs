using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddTestCaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCases_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestCaseId = table.Column<int>(type: "integer", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    InstructionVersionId = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedResponse = table.Column<string>(type: "text", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    ExecutionName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseRuns_AgentInstructionVersions_InstructionVersionId",
                        column: x => x.InstructionVersionId,
                        principalTable: "AgentInstructionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestCaseRuns_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestCaseRuns_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRunMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestCaseRunId = table.Column<int>(type: "integer", nullable: false),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvaluatorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRunMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseRunMetrics_Evaluators_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Evaluators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestCaseRunMetrics_TestCaseRuns_TestCaseRunId",
                        column: x => x.TestCaseRunId,
                        principalTable: "TestCaseRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRunMetrics_EvaluatorId",
                table: "TestCaseRunMetrics",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRunMetrics_TestCaseRunId",
                table: "TestCaseRunMetrics",
                column: "TestCaseRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRuns_AgentId",
                table: "TestCaseRuns",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRuns_ExecutionName",
                table: "TestCaseRuns",
                column: "ExecutionName");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRuns_InstructionVersionId",
                table: "TestCaseRuns",
                column: "InstructionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRuns_TestCaseId",
                table: "TestCaseRuns",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_MessageId",
                table: "TestCases",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestCaseRunMetrics");

            migrationBuilder.DropTable(
                name: "TestCaseRuns");

            migrationBuilder.DropTable(
                name: "TestCases");
        }
    }
}
