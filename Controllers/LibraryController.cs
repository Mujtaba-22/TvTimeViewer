using System.IO.Compression;
using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class LibraryViewModel
{
    public List<Show> Shows { get; set; } = new();
    public List<Show> WatchedShows { get; set; } = new();
    public List<Movie> Movies { get; set; } = new();
    public List<Movie> WatchedMovies { get; set; } = new();
}

public class LibraryController : Controller
{
    private readonly AppDbContext _db;
    private readonly DeduplicationService _dedup;
    private readonly PosterEnrichmentService _posters;
    private readonly GenreEnrichmentService _genres;
    private readonly ProgressTrackingService _tracker;
    private readonly IServiceScopeFactory _scopeFactory;

    public LibraryController(AppDbContext db, DeduplicationService dedup, PosterEnrichmentService posters,
        GenreEnrichmentService genres, ProgressTrackingService tracker, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _dedup = dedup;
        _posters = posters;
        _genres = genres;
        _tracker = tracker;
        _scopeFactory = scopeFactory;
    }

    public async Task<IActionResult> Index()
    {
        var showsWithEpisodes = await _db.Shows
            .Include(s => s.Episodes)
            .OrderBy(s => s.Title)
            .ToListAsync();

        var movies = await _db.Movies.OrderBy(m => m.Title).ToListAsync();

        var vm = new LibraryViewModel
        {
            Shows = showsWithEpisodes,
            WatchedShows = showsWithEpisodes
                .Where(s => s.Episodes.Any() && s.Episodes.All(e => e.Watched))
                .ToList(),
            Movies = movies,
            WatchedMovies = movies.Where(m => m.Watched).ToList()
        };
        return View(vm);
    }

    public async Task<IActionResult> AllShows(string? q, string? filter, string? genre, int page = 1)
    {
        const int pageSize = 24;
        var query = _db.Shows.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Title.Contains(q));

        query = filter switch
        {
            "following" => query.Where(s => s.Followed),
            "archived" => query.Where(s => s.Archived),
            "no-poster" => query.Where(s => s.PosterImage == null),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(s => s.Genre != null && s.Genre.Contains(genre));

        var totalShows = await query.CountAsync();
        var shows = await query.OrderBy(s => s.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalShows / (double)pageSize);
        ViewBag.Query = q;
        ViewBag.Filter = filter;
        ViewBag.Genre = genre;
        ViewBag.TotalResults = totalShows;
        ViewBag.AllGenres = await _genres.GetDistinctShowGenresAsync();

        return View(shows);
    }

    public async Task<IActionResult> AllWatchedShows(string? q, string? genre, int page = 1)
    {
        const int pageSize = 24;

        var showsWithEpisodes = await _db.Shows
            .Include(s => s.Episodes)
            .Where(s => s.Episodes.Any())
            .ToListAsync();

        var watchedShowIds = showsWithEpisodes
            .Where(s => s.Episodes.All(e => e.Watched))
            .Select(s => s.Id)
            .ToList();

        var query = _db.Shows.Where(s => watchedShowIds.Contains(s.Id));

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Title.Contains(q));

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(s => s.Genre != null && s.Genre.Contains(genre));

        var totalShows = await query.CountAsync();
        var shows = await query.OrderBy(s => s.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalShows / (double)pageSize);
        ViewBag.Query = q;
        ViewBag.Genre = genre;
        ViewBag.TotalResults = totalShows;
        ViewBag.AllGenres = await _genres.GetDistinctShowGenresAsync();

        return View(shows);
    }

    public async Task<IActionResult> AllMovies(string? q, string? filter, string? genre, int page = 1)
    {
        const int pageSize = 24;
        var query = _db.Movies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => m.Title.Contains(q));

        query = filter switch
        {
            "watched" => query.Where(m => m.Watched),
            "unwatched" => query.Where(m => !m.Watched),
            "no-poster" => query.Where(m => m.PosterImage == null),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));

        var totalMovies = await query.CountAsync();
        var movies = await query.OrderBy(m => m.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);
        ViewBag.Query = q;
        ViewBag.Filter = filter;
        ViewBag.Genre = genre;
        ViewBag.TotalResults = totalMovies;
        ViewBag.AllGenres = await _genres.GetDistinctMovieGenresAsync();

        return View(movies);
    }

    public async Task<IActionResult> AllWatchedMovies(string? q, string? genre, int page = 1)
    {
        const int pageSize = 24;
        var query = _db.Movies.Where(m => m.Watched);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => m.Title.Contains(q));

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));

        var totalMovies = await query.CountAsync();
        var movies = await query.OrderByDescending(m => m.WatchedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalMovies / (double)pageSize);
        ViewBag.Query = q;
        ViewBag.Genre = genre;
        ViewBag.TotalResults = totalMovies;
        ViewBag.AllGenres = await _genres.GetDistinctMovieGenresAsync();

        return View(movies);
    }

    [HttpPost]
    public async Task<IActionResult> StartImportZip(IFormFile zipFile)
    {
        if (zipFile == null || zipFile.Length == 0)
            return Json(new { error = "No ZIP file selected." });

        using var ms = new MemoryStream();
        await zipFile.CopyToAsync(ms);
        var zipBytes = ms.ToArray();

        var jobId = _tracker.CreateJob();
        _ = Task.Run(async () => await RunImportJob(jobId, zipBytes));

        return Json(new { jobId });
    }

    private async Task RunImportJob(string jobId, byte[] zipBytes)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dedup = scope.ServiceProvider.GetRequiredService<DeduplicationService>();
        var posters = scope.ServiceProvider.GetRequiredService<PosterEnrichmentService>();
        var genres = scope.ServiceProvider.GetRequiredService<GenreEnrichmentService>();

        try
        {
            int showCount = 0, movieCount = 0, skippedCount = 0, processedFiles = 0;

            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).ToList();
            int entryIndex = 0;

            foreach (var entry in entries)
            {
                entryIndex++;
                int parsePercent = (int)((entryIndex / (double)Math.Max(entries.Count, 1)) * 30);
                _tracker.Update(jobId, parsePercent, $"Parsing {entry.Name}...");

                var fileName = entry.Name.ToLowerInvariant();

                if (ImportRules.SensitiveFiles.Contains(fileName)) { skippedCount++; continue; }
                if (!ImportRules.SafeImportFiles.Contains(fileName)) { skippedCount++; continue; }

                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();
                bool hasSeriesCol = headers.Contains("tv_show_name") || headers.Contains("series_name");
                bool hasMovieCol = headers.Contains("movie_name");

                while (csv.Read())
                {
                    if (hasMovieCol)
                    {
                        var movieName = TryGet(csv, "movie_name");
                        if (!string.IsNullOrWhiteSpace(movieName))
                        {
                            if (!await db.Movies.AnyAsync(m => m.Title == movieName))
                            {
                                db.Movies.Add(new Movie
                                {
                                    Title = movieName,
                                    Watched = true,
                                    WatchedAt = DateTime.TryParse(TryGet(csv, "watch_date"), out var wd) ? wd : null
                                });
                                movieCount++;
                            }
                            continue;
                        }
                    }

                    if (hasSeriesCol)
                    {
                        var title = TryGet(csv, "tv_show_name") ?? TryGet(csv, "series_name");
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        if (!await db.Shows.AnyAsync(s => s.Title == title))
                        {
                            var tvShowId = int.TryParse(TryGet(csv, "tv_show_id"), out var id) ? id : 0;
                            db.Shows.Add(new Show
                            {
                                TvShowId = tvShowId,
                                Title = title,
                                Followed = TryGet(csv, "active") == "1",
                                Archived = TryGet(csv, "archived") == "1"
                            });
                            showCount++;
                        }
                    }
                }

                processedFiles++;
            }

            await db.SaveChangesAsync();

            _tracker.Update(jobId, 35, "Removing duplicate titles...");
            var (showsRemoved, moviesRemoved) = await dedup.RemoveDuplicatesAsync();

            _tracker.Update(jobId, 40, "Fetching posters...");
            var (showsWithPosters, moviesWithPosters) = await posters.EnrichAllAsync((cur, total) =>
            {
                int percent = 40 + (int)((cur / (double)Math.Max(total, 1)) * 30);
                _tracker.Update(jobId, percent, $"Fetching posters ({cur}/{total})...");
            });

            _tracker.Update(jobId, 70, "Fetching genres...");
            var (showsWithGenres, moviesWithGenres) = await genres.EnrichGenresAsync((cur, total) =>
            {
                int percent = 70 + (int)((cur / (double)Math.Max(total, 1)) * 30);
                _tracker.Update(jobId, percent, $"Fetching genres ({cur}/{total})...");
            });

            var message = processedFiles > 0
                ? $"Imported {showCount} shows and {movieCount} movies. Removed {showsRemoved} duplicate(s). " +
                  $"Fetched posters for {showsWithPosters}/{moviesWithPosters}. Fetched genres for {showsWithGenres}/{moviesWithGenres}. Skipped {skippedCount} file(s)."
                : "No recognized CSV files were found inside this ZIP.";

            _tracker.Complete(jobId, message);
        }
        catch (Exception ex)
        {
            _tracker.Fail(jobId, ex.Message);
        }
    }

    [HttpPost]
    public IActionResult StartRefreshPosters()
    {
        var jobId = _tracker.CreateJob();
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var posters = scope.ServiceProvider.GetRequiredService<PosterEnrichmentService>();
            try
            {
                var (showsUpdated, moviesUpdated) = await posters.EnrichAllAsync((cur, total) =>
                {
                    int percent = (int)((cur / (double)Math.Max(total, 1)) * 100);
                    _tracker.Update(jobId, percent, $"Fetching posters ({cur}/{total})...");
                });
                _tracker.Complete(jobId, $"Fetched posters for {showsUpdated} show(s) and {moviesUpdated} movie(s).");
            }
            catch (Exception ex) { _tracker.Fail(jobId, ex.Message); }
        });
        return Json(new { jobId });
    }

    [HttpPost]
    public IActionResult StartRefreshGenres()
    {
        var jobId = _tracker.CreateJob();
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var genres = scope.ServiceProvider.GetRequiredService<GenreEnrichmentService>();
            try
            {
                var (showsUpdated, moviesUpdated) = await genres.EnrichGenresAsync((cur, total) =>
                {
                    int percent = (int)((cur / (double)Math.Max(total, 1)) * 100);
                    _tracker.Update(jobId, percent, $"Fetching genres ({cur}/{total})...");
                });
                _tracker.Complete(jobId, $"Fetched genres for {showsUpdated} show(s) and {moviesUpdated} movie(s).");
            }
            catch (Exception ex) { _tracker.Fail(jobId, ex.Message); }
        });
        return Json(new { jobId });
    }

    [HttpPost]
    public IActionResult StartCleanDuplicates()
    {
        var jobId = _tracker.CreateJob();
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dedup = scope.ServiceProvider.GetRequiredService<DeduplicationService>();
            try
            {
                _tracker.Update(jobId, 50, "Removing duplicates...");
                var (showsRemoved, moviesRemoved) = await dedup.RemoveDuplicatesAsync();
                _tracker.Complete(jobId, $"Removed {showsRemoved} duplicate show(s) and {moviesRemoved} duplicate movie(s).");
            }
            catch (Exception ex) { _tracker.Fail(jobId, ex.Message); }
        });
        return Json(new { jobId });
    }

    [HttpGet]
    public IActionResult Progress(string id)
    {
        var state = _tracker.Get(id);
        if (state == null) return NotFound();
        return Json(state);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFollowed(int id)
    {
        var show = await _db.Shows.FindAsync(id);
        if (show != null)
        {
            show.Followed = !show.Followed;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteShow(int id)
    {
        var show = await _db.Shows.FindAsync(id);
        if (show != null) { _db.Shows.Remove(show); await _db.SaveChangesAsync(); }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie != null) { _db.Movies.Remove(movie); await _db.SaveChangesAsync(); }
        return RedirectToAction("Index");
    }

    private string? TryGet(CsvReader csv, string field)
    {
        try { return csv.GetField(field); } catch { return null; }
    }
}