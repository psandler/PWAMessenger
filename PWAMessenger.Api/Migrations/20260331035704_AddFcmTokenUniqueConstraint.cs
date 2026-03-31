using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PWAMessenger.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFcmTokenUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FcmTokens_UserId",
                table: "FcmTokens");

            migrationBuilder.CreateIndex(
                name: "IX_FcmTokens_UserId_Token",
                table: "FcmTokens",
                columns: new[] { "UserId", "Token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FcmTokens_UserId_Token",
                table: "FcmTokens");

            migrationBuilder.CreateIndex(
                name: "IX_FcmTokens_UserId",
                table: "FcmTokens",
                column: "UserId");
        }
    }
}
