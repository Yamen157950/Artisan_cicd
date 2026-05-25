using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtisanApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderProfileId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ProviderProfileId",
                table: "ServiceRequests",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ProviderProfileId_Status",
                table: "ServiceRequests",
                columns: new[] { "ProviderProfileId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequests");
        }
    }
}
