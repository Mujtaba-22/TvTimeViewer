using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class AddComickHidToManga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComickHid",
                table: "Manga",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComickHid",
                table: "Manga");
        }
    }
}
