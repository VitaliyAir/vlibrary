using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FileCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cover_checked_at",
                table: "file_metas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "file_covers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_meta_id = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_covers", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_covers_file_metas_file_meta_id",
                        column: x => x.file_meta_id,
                        principalTable: "file_metas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_cover_checked_at",
                table: "file_metas",
                column: "cover_checked_at");

            migrationBuilder.CreateIndex(
                name: "ix_file_covers_file_meta_id",
                table: "file_covers",
                column: "file_meta_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_covers");

            migrationBuilder.DropIndex(
                name: "ix_file_metas_cover_checked_at",
                table: "file_metas");

            migrationBuilder.DropColumn(
                name: "cover_checked_at",
                table: "file_metas");
        }
    }
}
