using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryRedirects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "redirect_state",
                table: "categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "redirect_to_id",
                table: "categories",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_redirect_to_id",
                table: "categories",
                column: "redirect_to_id");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_categories_redirect_to_id",
                table: "categories",
                column: "redirect_to_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_categories_redirect_to_id",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_categories_redirect_to_id",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "redirect_state",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "redirect_to_id",
                table: "categories");
        }
    }
}
