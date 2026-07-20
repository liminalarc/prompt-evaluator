using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Feature = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CacheCreationTokens = table.Column<int>(type: "integer", nullable: false),
                    CacheReadTokens = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: true),
                    RequestId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_records_Feature",
                table: "ai_usage_records",
                column: "Feature");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_records_Model",
                table: "ai_usage_records",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_records_OccurredAt",
                table: "ai_usage_records",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_records_OrganizationId_OccurredAt",
                table: "ai_usage_records",
                columns: new[] { "OrganizationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_records");
        }
    }
}
