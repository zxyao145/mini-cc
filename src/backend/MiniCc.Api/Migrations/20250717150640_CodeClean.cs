using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCc.Api.Migrations
{
    /// <inheritdoc />
    public partial class CodeClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_CreatedAt",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_Url",
                table: "Articles");

            migrationBuilder.RenameColumn(
                name: "TextContentLength",
                table: "Articles",
                newName: "Length");

            migrationBuilder.RenameColumn(
                name: "OriginContent",
                table: "Articles",
                newName: "OriginalContent");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:zhparser", ",,");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Tags",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Highlights",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "Highlights",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Highlights",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "Articles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AccessKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AccessKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedAt",
                table: "AccessKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AccessKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccessKeys_Users_UserId",
                table: "AccessKeys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccessKeys_Users_UserId",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AccessKeys");

            migrationBuilder.RenameColumn(
                name: "OriginalContent",
                table: "Articles",
                newName: "OriginContent");

            migrationBuilder.RenameColumn(
                name: "Length",
                table: "Articles",
                newName: "TextContentLength");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:zhparser", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Tags",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Highlights",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "Highlights",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Highlights",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "Articles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CreatedAt",
                table: "Articles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url",
                unique: true);
        }
    }
}
