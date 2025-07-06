using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace MiniCc.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeFullTextSearchConfig : Migration
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
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" })
                .OldAnnotation("Npgsql:TsVectorConfig", "english")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Articles",
                type: "tsvector",
                nullable: false,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector")
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" })
                .OldAnnotation("Npgsql:TsVectorConfig", "mixed_zh_en")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "Title", "ReadableContent", "Author" });
        }
    }
}
