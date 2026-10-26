using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FileRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                table: "file_metas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_file_name",
                table: "file_metas");
        }
    }
}
