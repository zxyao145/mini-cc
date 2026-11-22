using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCc.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class M5_ImageStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Images",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImagesDueAt",
                table: "Articles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ImagesDueAt",
                table: "Articles",
                column: "ImagesDueAt",
                filter: "\"ImagesDueAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_ImagesDueAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Images",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ImagesDueAt",
                table: "Articles");
        }
    }
}
