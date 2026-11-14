using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCc.Api.Migrations
{
    /// <inheritdoc />
    public partial class M4_ContentRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentRevision",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentRevision",
                table: "Articles");
        }
    }
}
