using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapperTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfflineKnowledgeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_ingestion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_ingestion_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalLocator = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RetentionDays = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StoredRelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_knowledge_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "knowledge_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    StartCharacter = table.Column<int>(type: "INTEGER", nullable: false),
                    EndCharacter = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_knowledge_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "knowledge_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document_tags",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document_tags", x => new { x.DocumentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_knowledge_document_tags_knowledge_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "knowledge_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_document_tags_knowledge_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "knowledge_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_DocumentId_Ordinal",
                table: "knowledge_chunks",
                columns: new[] { "DocumentId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_tags_TagId",
                table: "knowledge_document_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_ContentHash",
                table: "knowledge_documents",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_DeletedAt",
                table: "knowledge_documents",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_SourceId",
                table: "knowledge_documents",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_ingestion_jobs_Status",
                table: "knowledge_ingestion_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_tags_Name",
                table: "knowledge_tags",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("CREATE VIRTUAL TABLE knowledge_chunks_fts USING fts5(Text, content='knowledge_chunks', content_rowid='Id');");
            migrationBuilder.Sql("CREATE TRIGGER knowledge_chunks_ai AFTER INSERT ON knowledge_chunks BEGIN INSERT INTO knowledge_chunks_fts(rowid, Text) VALUES (new.Id, new.Text); END;");
            migrationBuilder.Sql("CREATE TRIGGER knowledge_chunks_ad AFTER DELETE ON knowledge_chunks BEGIN INSERT INTO knowledge_chunks_fts(knowledge_chunks_fts, rowid, Text) VALUES ('delete', old.Id, old.Text); END;");
            migrationBuilder.Sql("CREATE TRIGGER knowledge_chunks_au AFTER UPDATE ON knowledge_chunks BEGIN INSERT INTO knowledge_chunks_fts(knowledge_chunks_fts, rowid, Text) VALUES ('delete', old.Id, old.Text); INSERT INTO knowledge_chunks_fts(rowid, Text) VALUES (new.Id, new.Text); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS knowledge_chunks_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS knowledge_chunks_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS knowledge_chunks_ai;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS knowledge_chunks_fts;");

            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_document_tags");

            migrationBuilder.DropTable(
                name: "knowledge_ingestion_jobs");

            migrationBuilder.DropTable(
                name: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "knowledge_tags");

            migrationBuilder.DropTable(
                name: "knowledge_sources");
        }
    }
}
