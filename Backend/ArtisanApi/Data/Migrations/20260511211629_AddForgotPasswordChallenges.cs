using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtisanApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForgotPasswordChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForgotPasswordChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmailNormalized = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OtpCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgotPasswordChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForgotPasswordChallenges_EmailNormalized",
                table: "ForgotPasswordChallenges",
                column: "EmailNormalized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForgotPasswordChallenges");
        }
    }
}
