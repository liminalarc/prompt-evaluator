using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCatalog : Migration
    {
        // Well-known ids so the seed is idempotent across environments (matches the Default-org seed
        // pattern). Model ids verified via the claude-api skill; Anthropic prices are $ per MTok
        // (input/output). OpenAI prices are left null (no authoritative source; pricing is
        // display-only). All seeded models serve every role.
        private static readonly Guid OpusId = new("a0000000-0000-0000-0000-000000000001");
        private static readonly Guid SonnetId = new("a0000000-0000-0000-0000-000000000002");
        private static readonly Guid HaikuId = new("a0000000-0000-0000-0000-000000000003");
        private static readonly Guid Gpt4oId = new("a0000000-0000-0000-0000-000000000004");
        private static readonly Guid Gpt4oMiniId = new("a0000000-0000-0000-0000-000000000005");

        private static readonly string[] SeedColumns =
        {
            "Id", "ModelId", "DisplayName", "Provider",
            "CanSubject", "CanJudge", "CanGenerate",
            "InputPricePerMTokUsd", "OutputPricePerMTokUsd", "IsActive",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_catalog_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    CanSubject = table.Column<bool>(type: "boolean", nullable: false),
                    CanJudge = table.Column<bool>(type: "boolean", nullable: false),
                    CanGenerate = table.Column<bool>(type: "boolean", nullable: false),
                    InputPricePerMTokUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    OutputPricePerMTokUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_catalog_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model_catalog_entries_ModelId",
                table: "model_catalog_entries",
                column: "ModelId",
                unique: true);

            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { OpusId, "claude-opus-4-8", "Claude Opus 4.8", "Anthropic", true, true, true, 5m, 25m, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { SonnetId, "claude-sonnet-5", "Claude Sonnet 5", "Anthropic", true, true, true, 3m, 15m, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { HaikuId, "claude-haiku-4-5", "Claude Haiku 4.5", "Anthropic", true, true, true, 1m, 5m, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { Gpt4oId, "gpt-4o", "GPT-4o", "OpenAi", true, true, true, null, null, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { Gpt4oMiniId, "gpt-4o-mini", "GPT-4o mini", "OpenAi", true, true, true, null, null, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_catalog_entries");
        }
    }
}
