using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class AddMangaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Manga",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AniListId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CoverUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChaptersRead = table.Column<int>(type: "int", nullable: false),
                    TotalChapters = table.Column<int>(type: "int", nullable: true),
                    Following = table.Column<bool>(type: "bit", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manga", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Manga");
        }
    }
}
