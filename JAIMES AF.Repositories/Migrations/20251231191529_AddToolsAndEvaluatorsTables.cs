using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MattEland.Jaimes.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddToolsAndEvaluatorsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("TRUNCATE TABLE \"MessageToolCalls\" CASCADE;");
            migrationBuilder.Sql("TRUNCATE TABLE \"MessageEvaluationMetrics\" CASCADE;");

            migrationBuilder.AddColumn<int>(
                name: "ToolId",
                table: "MessageToolCalls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluatorId",
                table: "MessageEvaluationMetrics",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Evaluators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Evaluators", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "Tools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Tools", x => x.Id); });

            migrationBuilder.CreateIndex(
                name: "IX_MessageToolCalls_ToolId",
                table: "MessageToolCalls",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEvaluationMetrics_EvaluatorId",
                table: "MessageEvaluationMetrics",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluators_Name",
                table: "Evaluators",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_Name",
                table: "Tools",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageEvaluationMetrics_Evaluators_EvaluatorId",
                table: "MessageEvaluationMetrics",
                column: "EvaluatorId",
                principalTable: "Evaluators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageToolCalls_Tools_ToolId",
                table: "MessageToolCalls",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageEvaluationMetrics_Evaluators_EvaluatorId",
                table: "MessageEvaluationMetrics");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageToolCalls_Tools_ToolId",
                table: "MessageToolCalls");

            migrationBuilder.DropTable(
                name: "Evaluators");

            migrationBuilder.DropTable(
                name: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_MessageToolCalls_ToolId",
                table: "MessageToolCalls");

            migrationBuilder.DropIndex(
                name: "IX_MessageEvaluationMetrics_EvaluatorId",
                table: "MessageEvaluationMetrics");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "MessageToolCalls");

            migrationBuilder.DropColumn(
                name: "EvaluatorId",
                table: "MessageEvaluationMetrics");
        }
    }
}
