using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtisanApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestProviderUserIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProviderUserId",
                table: "ServiceRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ProviderUserId",
                table: "ServiceRequests",
                column: "ProviderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ProviderUserId",
                table: "ServiceRequests");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderUserId",
                table: "ServiceRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
