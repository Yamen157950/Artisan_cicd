using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtisanApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatThreadReadStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatThreadReadStates",
                columns: table => new
                {
                    ReaderUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    PartnerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    LastReadAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreadReadStates", x => new { x.ReaderUserId, x.PartnerUserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreadReadStates_ReaderUserId",
                table: "ChatThreadReadStates",
                column: "ReaderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatThreadReadStates");
        }
    }
}
