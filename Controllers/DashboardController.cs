using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Controllers;

public class ActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime When { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

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

    public int TotalManga { get; set; }
    public int CompletedManga { get; set; }
    public int TotalChaptersRead { get; set; }

    public int TotalGames { get; set; }
    public int CompletedGames { get; set; }
    public int PlayingGames { get; set; }
    public double TotalHoursPlayed { get; set; }

    public List<Show> TopUnwatchedShows { get; set; } = new();
    public List<Manga> InProgressManga { get; set; } = new();
    public List<Game> InProgressGames { get; set; } = new();

    public List<ActivityItem> RecentActivity { get; set; } = new();
}

public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var shows = await _db.Shows.Include(s => s.Episodes).ToListAsync();
        var movies = await _db.Movies.ToListAsync();
        var manga = await _db.Manga.ToListAsync();
        var games = await _db.Games.ToListAsync();

        var vm = new DashboardViewModel
        {
            TotalShows = shows.Count,
            FollowedShows = shows.Count(s => s.Followed),
            ArchivedShows = shows.Count(s => s.Archived),
            TotalMovies = movies.Count,
            WatchedMovies = movies.Count(m => m.Watched),
            TotalEpisodes = shows.Sum(s => s.Episodes.Count),
            WatchedEpisodes = shows.Sum(s => s.Episodes.Count(e => e.Watched)),
            MissingShowPosters = shows.Count(s => s.PosterImage == null),
            MissingMoviePosters = movies.Count(m => m.PosterImage == null),

            TotalManga = manga.Count,
            CompletedManga = manga.Count(m => m.Completed),
            TotalChaptersRead = manga.Sum(m => m.ChaptersRead),

            TotalGames = games.Count,
            CompletedGames = games.Count(g => g.Completed),
            PlayingGames = games.Count(g => g.Playing),
            TotalHoursPlayed = games.Sum(g => g.HoursPlayed),

            TopUnwatchedShows = shows
                .Where(s => s.Followed && s.Episodes.Any(e => !e.Watched))
                .OrderByDescending(s => s.Episodes.Count(e => !e.Watched))
                .Take(8)
                .ToList(),

            InProgressManga = manga
                .Where(m => !m.Completed && m.ChaptersRead > 0)
                .OrderByDescending(m => m.LastReadAt)
                .Take(8)
                .ToList(),

            InProgressGames = games
                .Where(g => g.Playing)
                .OrderByDescending(g => g.LastPlayedAt)
                .Take(8)
                .ToList()
        };

        var activity = new List<ActivityItem>();

        activity.AddRange(shows.Where(s => s.LastWatchedAt != null).Select(s => new ActivityItem
        {
            Title = s.Title,
            Type = "Show",
            When = s.LastWatchedAt!.Value,
            Url = Url.Action("Details", "Show", new { id = s.Id })!,
            Icon = "📺"
        }));

        activity.AddRange(movies.Where(m => m.WatchedAt != null).Select(m => new ActivityItem
        {
            Title = m.Title,
            Type = "Movie",
            When = m.WatchedAt!.Value,
            Url = Url.Action("Details", "Movie", new { id = m.Id })!,
            Icon = "🎬"
        }));

        activity.AddRange(manga.Where(m => m.LastReadAt != null).Select(m => new ActivityItem
        {
            Title = m.Title,
            Type = "Manga",
            When = m.LastReadAt!.Value,
            Url = Url.Action("Details", "Manga", new { id = m.Id })!,
            Icon = "📖"
        }));

        activity.AddRange(games.Where(g => g.LastPlayedAt != null).Select(g => new ActivityItem
        {
            Title = g.Title,
            Type = "Game",
            When = g.LastPlayedAt!.Value,
            Url = Url.Action("Details", "Games", new { id = g.Id })!,
            Icon = "🎮"
        }));

        vm.RecentActivity = activity.OrderByDescending(a => a.When).Take(12).ToList();

        return View(vm);
    }
}