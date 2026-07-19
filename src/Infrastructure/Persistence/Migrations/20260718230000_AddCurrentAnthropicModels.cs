using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentAnthropicModels : Migration
    {
        // 1.19 — current, active Anthropic models missing from the 1.13 seed. Golf (5.1) runs prompts on
        // Sonnet 4.6, which wasn't selectable as a target model. Well-known ids keep the seed idempotent,
        // matching the 1.13 pattern (never edit the applied AddModelCatalog migration). Model ids + prices
        // ($ per MTok input/output) verified via the claude-api skill. All three serve every role.
        private static readonly Guid Sonnet46Id = new("a0000000-0000-0000-0000-000000000006");
        private static readonly Guid Opus47Id = new("a0000000-0000-0000-0000-000000000007");
        private static readonly Guid Opus46Id = new("a0000000-0000-0000-0000-000000000008");

        private static readonly string[] SeedColumns =
        {
            "Id", "ModelId", "DisplayName", "Provider",
            "CanSubject", "CanJudge", "CanGenerate",
            "InputPricePerMTokUsd", "OutputPricePerMTokUsd", "IsActive",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { Sonnet46Id, "claude-sonnet-4-6", "Claude Sonnet 4.6", "Anthropic", true, true, true, 3m, 15m, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { Opus47Id, "claude-opus-4-7", "Claude Opus 4.7", "Anthropic", true, true, true, 5m, 25m, true });
            migrationBuilder.InsertData(table: "model_catalog_entries", columns: SeedColumns,
                values: new object[] { Opus46Id, "claude-opus-4-6", "Claude Opus 4.6", "Anthropic", true, true, true, 5m, 25m, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "model_catalog_entries", keyColumn: "Id", keyValue: Sonnet46Id);
            migrationBuilder.DeleteData(table: "model_catalog_entries", keyColumn: "Id", keyValue: Opus47Id);
            migrationBuilder.DeleteData(table: "model_catalog_entries", keyColumn: "Id", keyValue: Opus46Id);
        }
    }
}
