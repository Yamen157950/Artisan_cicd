using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtisanApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "DirectMessages",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentOriginalName",
                table: "DirectMessages",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSizeBytes",
                table: "DirectMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentStoredName",
                table: "DirectMessages",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentOriginalName",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentSizeBytes",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentStoredName",
                table: "DirectMessages");
        }
    }
}
