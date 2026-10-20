using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace VLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "action_logs",
                columns: table => new
                {
                    action_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    action_by_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    entity_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_action_logs", x => x.action_log_id);
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "authors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name_initials = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    biography = table.Column<string>(type: "text", nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_clasificators",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "bookshelves",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookshelves", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title_words_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    isbn = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    edition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    publisher = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    year_published = table.Column<int>(type: "integer", nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: true),
                    language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    cover_image = table.Column<byte[]>(type: "bytea", nullable: true),
                    cover_image_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    table_of_contents = table.Column<string>(type: "text", nullable: true),
                    glossary = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    reading_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    search_text = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    book_clasificator_id = table.Column<long>(type: "bigint", nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian'::regconfig, coalesce(title, '') || ' ' || coalesce(subtitle, '') || ' ' || coalesce(isbn, '') || ' ' || coalesce(search_text, ''))", stored: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_books", x => x.id);
                    table.CheckConstraint("ck_books_rating", "rating IS NULL OR rating BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_books_book_clasificators_book_clasificator_id",
                        column: x => x.book_clasificator_id,
                        principalTable: "book_clasificators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    parent_category_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    total_files = table.Column<int>(type: "integer", nullable: false),
                    processed_files = table.Column<int>(type: "integer", nullable: false),
                    unchanged_count = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    already_in_library_count = table.Column<int>(type: "integer", nullable: false),
                    conflict_count = table.Column<int>(type: "integer", nullable: false),
                    deleted_duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    known_deleted_count = table.Column<int>(type: "integer", nullable: false),
                    pending_review_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_batches_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "book_authors",
                columns: table => new
                {
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_authors", x => new { x.book_id, x.author_id });
                    table.ForeignKey(
                        name: "fk_book_authors_authors_author_id",
                        column: x => x.author_id,
                        principalTable: "authors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_book_authors_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_tags",
                columns: table => new
                {
                    books_id = table.Column<long>(type: "bigint", nullable: false),
                    tags_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_tags", x => new { x.books_id, x.tags_id });
                    table.ForeignKey(
                        name: "fk_book_tags_books_books_id",
                        column: x => x.books_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_tags_tags_tags_id",
                        column: x => x.tags_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookshelf_books",
                columns: table => new
                {
                    bookshelf_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookshelf_books", x => new { x.bookshelf_id, x.book_id });
                    table.ForeignKey(
                        name: "fk_bookshelf_books_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bookshelf_books_bookshelves_bookshelf_id",
                        column: x => x.bookshelf_id,
                        principalTable: "bookshelves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "file_metas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    last_write_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    storage_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content_title_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    book_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    extracted_metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_metas", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_metas_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_file_metas_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_notes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    file_meta_id = table.Column<long>(type: "bigint", nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    review_box = table.Column<int>(type: "integer", nullable: false),
                    next_review_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian'::regconfig, coalesce(text, ''))", stored: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_notes", x => x.id);
                    table.CheckConstraint("ck_book_notes_review_box", "review_box BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_book_notes_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_book_notes_file_metas_file_meta_id",
                        column: x => x.file_meta_id,
                        principalTable: "file_metas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "duplicate_candidates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_meta_id = table.Column<long>(type: "bigint", nullable: false),
                    candidate_book_id = table.Column<long>(type: "bigint", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duplicate_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_duplicate_candidates_books_candidate_book_id",
                        column: x => x.candidate_book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_duplicate_candidates_file_metas_file_meta_id",
                        column: x => x.file_meta_id,
                        principalTable: "file_metas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    import_batch_id = table.Column<long>(type: "bigint", nullable: false),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_meta_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_items_file_metas_file_meta_id",
                        column: x => x.file_meta_id,
                        principalTable: "file_metas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_import_items_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_action_logs_action",
                table: "action_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_action_logs_action_by_id",
                table: "action_logs",
                column: "action_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_action_logs_entity_name",
                table: "action_logs",
                column: "entity_name");

            migrationBuilder.CreateIndex(
                name: "ix_action_logs_timestamp",
                table: "action_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_authors_last_name",
                table: "authors",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_authors_normalized_name",
                table: "authors",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "ix_book_authors_author_id",
                table: "book_authors",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_clasificators_parent_id",
                table: "book_clasificators",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_notes_book_id",
                table: "book_notes",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_notes_file_meta_id",
                table: "book_notes",
                column: "file_meta_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_notes_next_review_at",
                table: "book_notes",
                column: "next_review_at");

            migrationBuilder.CreateIndex(
                name: "ix_book_notes_search_vector",
                table: "book_notes",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_book_tags_tags_id",
                table: "book_tags",
                column: "tags_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_book_clasificator_id",
                table: "books",
                column: "book_clasificator_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_isbn",
                table: "books",
                column: "isbn");

            migrationBuilder.CreateIndex(
                name: "ix_books_last_opened_at",
                table: "books",
                column: "last_opened_at");

            migrationBuilder.CreateIndex(
                name: "ix_books_normalized_title",
                table: "books",
                column: "normalized_title");

            migrationBuilder.CreateIndex(
                name: "ix_books_search_vector",
                table: "books",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_books_title",
                table: "books",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_books_title_words_key",
                table: "books",
                column: "title_words_key");

            migrationBuilder.CreateIndex(
                name: "ix_bookshelf_books_book_id",
                table: "bookshelf_books",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id_name_key",
                table: "categories",
                columns: new[] { "parent_id", "name_key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_categories_path",
                table: "categories",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "ix_duplicate_candidates_candidate_book_id",
                table: "duplicate_candidates",
                column: "candidate_book_id");

            migrationBuilder.CreateIndex(
                name: "ix_duplicate_candidates_file_meta_id_candidate_book_id",
                table: "duplicate_candidates",
                columns: new[] { "file_meta_id", "candidate_book_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_book_id",
                table: "file_metas",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_category_id_file_name",
                table: "file_metas",
                columns: new[] { "category_id", "file_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_content_title_key",
                table: "file_metas",
                column: "content_title_key");

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_extension_size_bytes",
                table: "file_metas",
                columns: new[] { "extension", "size_bytes" });

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_file_hash",
                table: "file_metas",
                column: "file_hash");

            migrationBuilder.CreateIndex(
                name: "ix_file_metas_storage_object_id",
                table: "file_metas",
                column: "storage_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_parent_category_id",
                table: "import_batches",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_status",
                table: "import_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_import_items_file_meta_id",
                table: "import_items",
                column: "file_meta_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_items_import_batch_id_outcome",
                table: "import_items",
                columns: new[] { "import_batch_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_tags_normalized_name",
                table: "tags",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_logs");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "book_authors");

            migrationBuilder.DropTable(
                name: "book_notes");

            migrationBuilder.DropTable(
                name: "book_tags");

            migrationBuilder.DropTable(
                name: "bookshelf_books");

            migrationBuilder.DropTable(
                name: "duplicate_candidates");

            migrationBuilder.DropTable(
                name: "import_items");

            migrationBuilder.DropTable(
                name: "authors");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "bookshelves");

            migrationBuilder.DropTable(
                name: "file_metas");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "book_clasificators");
        }
    }
}
