using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptCurrentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentVersionId",
                table: "prompts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CurrentVersionSetAt",
                table: "prompts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentVersionSha",
                table: "prompts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentVersionId",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "CurrentVersionSetAt",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "CurrentVersionSha",
                table: "prompts");
        }
    }
}
