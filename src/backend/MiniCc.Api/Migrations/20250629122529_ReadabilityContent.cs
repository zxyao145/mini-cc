using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmeReader.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReadabilityContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Articles",
                newName: "ReadableContent");

            migrationBuilder.AddColumn<string>(
                name: "OriginContent",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TextContentLegth",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginContent",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "TextContentLegth",
                table: "Articles");

            migrationBuilder.RenameColumn(
                name: "ReadableContent",
                table: "Articles",
                newName: "Content");
        }
    }
}
