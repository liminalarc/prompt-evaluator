using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizations : Migration
    {
        // Well-known id for the seeded "Default" organization that migrated + fresh-DB data
        // belongs to (1.9). Existing folders/prompts backfill to it via the column default below.
        private static readonly Guid DefaultOrgId = new("11111111-1111-1111-1111-111111111111");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The organizations table must exist before anything can reference it.
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            // 2. Seed the "Default" org so every DB (fresh or migrated) has one to show and to
            //    backfill existing rows into.
            migrationBuilder.InsertData(
                table: "organizations",
                columns: new[] { "Id", "Name" },
                values: new object[] { DefaultOrgId, "Default" });

            // 3. Add the FK columns; the default backfills any pre-existing rows to the Default org.
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "prompts",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultOrgId);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "folders",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultOrgId);

            migrationBuilder.CreateIndex(
                name: "IX_prompts_OrganizationId",
                table: "prompts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_folders_OrganizationId",
                table: "folders",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_folders_organizations_OrganizationId",
                table: "folders",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_prompts_organizations_OrganizationId",
                table: "prompts",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_folders_organizations_OrganizationId",
                table: "folders");

            migrationBuilder.DropForeignKey(
                name: "FK_prompts_organizations_OrganizationId",
                table: "prompts");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_prompts_OrganizationId",
                table: "prompts");

            migrationBuilder.DropIndex(
                name: "IX_folders_OrganizationId",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "folders");
        }
    }
}
