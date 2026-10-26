using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_books_book_clasificators_book_clasificator_id",
                table: "books");

            migrationBuilder.DropTable(
                name: "book_clasificators");

            migrationBuilder.DropIndex(
                name: "ix_books_book_clasificator_id",
                table: "books");

            migrationBuilder.DropColumn(
                name: "book_clasificator_id",
                table: "books");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "book_clasificator_id",
                table: "books",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "book_clasificators",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_clasificators", x => x.id);
                    table.ForeignKey(
                        name: "fk_book_clasificators_book_clasificators_parent_id",
                        column: x => x.parent_id,
                        principalTable: "book_clasificators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_books_book_clasificator_id",
                table: "books",
                column: "book_clasificator_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_clasificators_parent_id",
                table: "book_clasificators",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_books_book_clasificators_book_clasificator_id",
                table: "books",
                column: "book_clasificator_id",
                principalTable: "book_clasificators",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
