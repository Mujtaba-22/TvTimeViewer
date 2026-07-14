using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Models;

namespace TvTimeViewer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Show> Shows { get; set; }
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Episode> Episodes { get; set; }
    public DbSet<Manga> Manga { get; set; }
    public DbSet<MangaChapter> MangaChapters { get; set; }
    public DbSet<Game> Games { get; set; }
}