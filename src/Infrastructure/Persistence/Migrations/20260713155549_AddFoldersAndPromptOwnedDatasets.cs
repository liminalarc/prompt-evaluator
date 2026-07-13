using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldersAndPromptOwnedDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "prompts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromptId",
                table: "datasets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_folders_folders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_FolderId",
                table: "prompts",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_datasets_PromptId",
                table: "datasets",
                column: "PromptId");

            migrationBuilder.CreateIndex(
                name: "IX_folders_ParentId",
                table: "folders",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_datasets_prompts_PromptId",
                table: "datasets",
                column: "PromptId",
                principalTable: "prompts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_prompts_folders_FolderId",
                table: "prompts",
                column: "FolderId",
                principalTable: "folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_datasets_prompts_PromptId",
                table: "datasets");

            migrationBuilder.DropForeignKey(
                name: "FK_prompts_folders_FolderId",
                table: "prompts");

            migrationBuilder.DropTable(
                name: "folders");

            migrationBuilder.DropIndex(
                name: "IX_prompts_FolderId",
                table: "prompts");

            migrationBuilder.DropIndex(
                name: "IX_datasets_PromptId",
                table: "datasets");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "prompts");

            migrationBuilder.DropColumn(
                name: "PromptId",
                table: "datasets");
        }
    }
}
