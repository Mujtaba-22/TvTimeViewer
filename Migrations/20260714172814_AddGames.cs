using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IgdbId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CoverContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<double>(type: "float", nullable: true),
                    ReleaseYear = table.Column<int>(type: "int", nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    Playing = table.Column<bool>(type: "bit", nullable: false),
                    HoursPlayed = table.Column<double>(type: "float", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
