using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Controllers;

public class DashboardViewModel
{
    public int TotalShows { get; set; }
    public int FollowedShows { get; set; }
    public int ArchivedShows { get; set; }
    public int TotalMovies { get; set; }
    public int WatchedMovies { get; set; }
    public int TotalEpisodes { get; set; }
    public int WatchedEpisodes { get; set; }
    public int MissingShowPosters { get; set; }
    public int MissingMoviePosters { get; set; }
    public List<Show> RecentlyWatchedShows { get; set; } = new();
    public List<Movie> RecentlyWatchedMovies { get; set; } = new();
    public List<Show> TopUnwatchedShows { get; set; } = new();
}

public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel
        {
            TotalShows = await _db.Shows.CountAsync(),
            FollowedShows = await _db.Shows.CountAsync(s => s.Followed),
            ArchivedShows = await _db.Shows.CountAsync(s => s.Archived),
            TotalMovies = await _db.Movies.CountAsync(),
            WatchedMovies = await _db.Movies.CountAsync(m => m.Watched),
            TotalEpisodes = await _db.Episodes.CountAsync(),
            WatchedEpisodes = await _db.Episodes.CountAsync(e => e.Watched),
            MissingShowPosters = await _db.Shows.CountAsync(s => s.PosterImage == null),
            MissingMoviePosters = await _db.Movies.CountAsync(m => m.PosterImage == null),

            RecentlyWatchedShows = await _db.Shows
                .Where(s => s.LastWatchedAt != null)
                .OrderByDescending(s => s.LastWatchedAt)
                .Take(6)
                .ToListAsync(),

            RecentlyWatchedMovies = await _db.Movies
                .Where(m => m.WatchedAt != null)
                .OrderByDescending(m => m.WatchedAt)
                .Take(6)
                .ToListAsync(),

            TopUnwatchedShows = await _db.Shows
                .Include(s => s.Episodes)
                .Where(s => s.Followed && s.Episodes.Any(e => !e.Watched))
                .OrderByDescending(s => s.Episodes.Count(e => !e.Watched))
                .Take(6)
                .ToListAsync()
        };

        return View(vm);
    }
}