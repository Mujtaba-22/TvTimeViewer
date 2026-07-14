using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterAddedAtWithApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MangaDexId",
                table: "Manga",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MangaDexId",
                table: "Manga");
        }
    }
}
