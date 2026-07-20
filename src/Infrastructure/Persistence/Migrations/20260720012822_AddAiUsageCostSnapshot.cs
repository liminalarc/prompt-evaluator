using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageCostSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostUsd",
                table: "ai_usage_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PricingMissing",
                table: "ai_usage_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RateVersion",
                table: "ai_usage_records",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostUsd",
                table: "ai_usage_records");

            migrationBuilder.DropColumn(
                name: "PricingMissing",
                table: "ai_usage_records");

            migrationBuilder.DropColumn(
                name: "RateVersion",
                table: "ai_usage_records");
        }
    }
}
