using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class Upadate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Shows");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Shows",
                newName: "LastWatchedAt");

            migrationBuilder.RenameColumn(
                name: "IsArchived",
                table: "Shows",
                newName: "Followed");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Shows",
                newName: "Archived");

            migrationBuilder.RenameColumn(
                name: "IsWatched",
                table: "Movies",
                newName: "Watched");

            migrationBuilder.AddColumn<string>(
                name: "PosterUrl",
                table: "Shows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosterUrl",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WatchedAt",
                table: "Movies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AirDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Watched = table.Column<bool>(type: "bit", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_ShowId",
                table: "Episodes",
                column: "ShowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropColumn(
                name: "PosterUrl",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "PosterUrl",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "WatchedAt",
                table: "Movies");

            migrationBuilder.RenameColumn(
                name: "LastWatchedAt",
                table: "Shows",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Followed",
                table: "Shows",
                newName: "IsArchived");

            migrationBuilder.RenameColumn(
                name: "Archived",
                table: "Shows",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "Watched",
                table: "Movies",
                newName: "IsWatched");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Shows",
                type: "datetime2",
                nullable: true);
        }
    }
}
