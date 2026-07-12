using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TvTimeViewer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShowMovieModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "WatchCount",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "WatchedAt",
                table: "Movies");

            migrationBuilder.RenameColumn(
                name: "LastWatchedAt",
                table: "Shows",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "EpisodesWatched",
                table: "Shows",
                newName: "TvShowId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Shows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Shows",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Shows",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Shows");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Shows",
                newName: "LastWatchedAt");

            migrationBuilder.RenameColumn(
                name: "TvShowId",
                table: "Shows",
                newName: "EpisodesWatched");

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Shows",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Shows",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WatchCount",
                table: "Movies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WatchedAt",
                table: "Movies",
                type: "datetime2",
                nullable: true);
        }
    }
}
