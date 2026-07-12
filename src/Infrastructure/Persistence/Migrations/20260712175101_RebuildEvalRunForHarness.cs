using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildEvalRunForHarness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Output",
                table: "eval_runs");

            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "eval_runs");

            migrationBuilder.AddColumn<Guid>(
                name: "DatasetId",
                table: "eval_runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PromptId",
                table: "eval_runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PromptVersionId",
                table: "eval_runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "fixture_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FixtureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelOutput = table.Column<string>(type: "text", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    EvalRunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixture_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fixture_runs_eval_runs_EvalRunId",
                        column: x => x.EvalRunId,
                        principalTable: "eval_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    scorer_kind = table.Column<string>(type: "text", nullable: false),
                    scorer_config = table.Column<string>(type: "text", nullable: false),
                    judge_model = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    FixtureRunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scores_fixture_runs_FixtureRunId",
                        column: x => x.FixtureRunId,
                        principalTable: "fixture_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixture_runs_EvalRunId",
                table: "fixture_runs",
                column: "EvalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_scores_FixtureRunId",
                table: "scores",
                column: "FixtureRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scores");

            migrationBuilder.DropTable(
                name: "fixture_runs");

            migrationBuilder.DropColumn(
                name: "DatasetId",
                table: "eval_runs");

            migrationBuilder.DropColumn(
                name: "PromptId",
                table: "eval_runs");

            migrationBuilder.DropColumn(
                name: "PromptVersionId",
                table: "eval_runs");

            migrationBuilder.AddColumn<string>(
                name: "Output",
                table: "eval_runs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "eval_runs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
