using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbIdToShowAndMovie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImdbId",
                table: "Shows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImdbId",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Movies");
        }
    }
}
