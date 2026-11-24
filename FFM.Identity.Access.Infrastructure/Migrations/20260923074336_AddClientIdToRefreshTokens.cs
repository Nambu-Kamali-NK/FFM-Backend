using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FFM.Identity.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "RefreshTokens");
        }
    }
}
