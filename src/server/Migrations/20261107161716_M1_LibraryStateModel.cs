using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace MiniCc.Api.Migrations
{
    /// <inheritdoc />
    public partial class M1_LibraryStateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Articles",
                type: "tsvector",
                nullable: false,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector")
                .Annotation("Npgsql:TsVectorConfig", "mixed_zh_en")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "Summary", "ReadableContent", "Author" })
                .OldAnnotation("Npgsql:TsVectorConfig", "mixed_zh_en")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" });

            migrationBuilder.AddColumn<int>(
                name: "ContentKind",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReadAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUrl",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProgressBlockIndex",
                table: "Articles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProgressBlockRatio",
                table: "Articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ProgressPercent",
                table: "Articles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressSessionId",
                table: "Articles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTextOffset",
                table: "Articles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProgressUpdatedAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReadingStatus",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedFromExtensionAt",
                table: "Articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Articles"
                SET "ReadingStatus" = CASE WHEN "ReadAt" IS NOT NULL THEN 2 ELSE 0 END,
                    "SavedFromExtensionAt" = "CreatedAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_DeletedAt",
                table: "Articles",
                column: "DeletedAt",
                filter: "\"DeletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_DeletedAt_IsArchived_CreatedAt_Id",
                table: "Articles",
                columns: new[] { "DeletedAt", "IsArchived", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_LastReadAt",
                table: "Articles",
                columns: new[] { "LastReadAt", "Id" },
                descending: new bool[0])
                .Annotation("Npgsql:IndexNullSortOrder", new[] { NullSortOrder.NullsLast, NullSortOrder.Unspecified });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_NormalizedUrl",
                table: "Articles",
                column: "NormalizedUrl")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_PublishedAt",
                table: "Articles",
                columns: new[] { "PublishedAt", "Id" },
                descending: new bool[0])
                .Annotation("Npgsql:IndexNullSortOrder", new[] { NullSortOrder.NullsLast, NullSortOrder.Unspecified });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_DeletedAt",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_DeletedAt_IsArchived_CreatedAt_Id",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_LastReadAt",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_NormalizedUrl",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_PublishedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ContentKind",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "NormalizedUrl",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressBlockIndex",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressBlockRatio",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressSessionId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressTextOffset",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ProgressUpdatedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ReadingStatus",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SavedFromExtensionAt",
                table: "Articles");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Articles",
                type: "tsvector",
                nullable: false,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector")
                .Annotation("Npgsql:TsVectorConfig", "mixed_zh_en")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" })
                .OldAnnotation("Npgsql:TsVectorConfig", "mixed_zh_en")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "Title", "Summary", "ReadableContent", "Author" });
        }
    }
}
