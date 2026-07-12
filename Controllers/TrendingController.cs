using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Controllers;

public class TrendingController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;

    public TrendingController(IHttpClientFactory httpFactory, IConfiguration config, IMemoryCache cache, AppDbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _cache = cache;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Top()
    {
        const string cacheKey = "trending-top-10";

        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Json(cached!);

        var apiKey = _config["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Json(new { error = "No TMDb API key configured." });

        var client = _httpFactory.CreateClient("tmdb");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            var showTask = client.GetStringAsync(
                $"https://api.themoviedb.org/3/trending/tv/week?api_key={apiKey}", cts.Token);
            var movieTask = client.GetStringAsync(
                $"https://api.themoviedb.org/3/trending/movie/week?api_key={apiKey}", cts.Token);

            await Task.WhenAll(showTask, movieTask);

            var topShows = ExtractTopN(showTask.Result, isMovie: false, count: 10);
            var topMovies = ExtractTopN(movieTask.Result, isMovie: true, count: 10);

            var result = new { shows = topShows, movies = topMovies };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));

            return Json(result);
        }
        catch (OperationCanceledException)
        {
            return Json(new { error = "TMDb took too long to respond. Try again shortly." });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> TopRated()
    {
        const string cacheKey = "top-rated-all";

        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Json(cached!);

        var apiKey = _config["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Json(new { error = "No TMDb API key configured." });

        var client = _httpFactory.CreateClient("tmdb");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            var showTask = client.GetStringAsync(
                $"https://api.themoviedb.org/3/tv/top_rated?api_key={apiKey}", cts.Token);
            var movieTask = client.GetStringAsync(
                $"https://api.themoviedb.org/3/movie/top_rated?api_key={apiKey}", cts.Token);

            await Task.WhenAll(showTask, movieTask);

            var topShows = ExtractTopN(showTask.Result, isMovie: false, count: 10);
            var topMovies = ExtractTopN(movieTask.Result, isMovie: true, count: 10);

            var result = new { shows = topShows, movies = topMovies };

            _cache.Set(cacheKey, result, TimeSpan.FromHours(6));

            return Json(result);
        }
        catch (OperationCanceledException)
        {
            return Json(new { error = "TMDb took too long to respond. Try again shortly." });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddShow(int tmdbId)
    {
        var apiKey = _config["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Json(new { success = false, message = "No TMDb API key configured." });

        var client = _httpFactory.CreateClient("tmdb");

        try
        {
            var detailJson = await client.GetStringAsync(
                $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}");
            using var doc = JsonDocument.Parse(detailJson);
            var root = doc.RootElement;

            var title = root.GetProperty("name").GetString() ?? "Untitled";

            var existing = await _db.Shows.FirstOrDefaultAsync(s => s.Title == title);
            if (existing != null)
                return Json(new { success = false, message = $"\"{title}\" is already in your library." });

            var genres = root.TryGetProperty("genres", out var genresEl)
                ? string.Join(", ", genresEl.EnumerateArray().Select(g => g.GetProperty("name").GetString()))
                : null;

            byte[]? posterBytes = null;
            if (root.TryGetProperty("poster_path", out var posterEl) && posterEl.ValueKind == JsonValueKind.String)
            {
                var posterUrl = $"https://image.tmdb.org/t/p/w500{posterEl.GetString()}";
                posterBytes = await client.GetByteArrayAsync(posterUrl);
            }

            var show = new Show
            {
                TvShowId = tmdbId,
                Title = title,
                Genre = genres,
                PosterImage = posterBytes,
                Followed = false,
                Archived = false
            };

            _db.Shows.Add(show);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"\"{title}\" was added to your library." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddMovie(int tmdbId)
    {
        var apiKey = _config["Tmdb:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return Json(new { success = false, message = "No TMDb API key configured." });

        var client = _httpFactory.CreateClient("tmdb");

        try
        {
            var detailJson = await client.GetStringAsync(
                $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}");
            using var doc = JsonDocument.Parse(detailJson);
            var root = doc.RootElement;

            var title = root.GetProperty("title").GetString() ?? "Untitled";

            var existing = await _db.Movies.FirstOrDefaultAsync(m => m.Title == title);
            if (existing != null)
                return Json(new { success = false, message = $"\"{title}\" is already in your library." });

            var genres = root.TryGetProperty("genres", out var genresEl)
                ? string.Join(", ", genresEl.EnumerateArray().Select(g => g.GetProperty("name").GetString()))
                : null;

            byte[]? posterBytes = null;
            if (root.TryGetProperty("poster_path", out var posterEl) && posterEl.ValueKind == JsonValueKind.String)
            {
                var posterUrl = $"https://image.tmdb.org/t/p/w500{posterEl.GetString()}";
                posterBytes = await client.GetByteArrayAsync(posterUrl);
            }

            var movie = new Movie
            {
                Title = title,
                Genre = genres,
                PosterImage = posterBytes,
                Watched = false,
                WatchedAt = null
            };

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"\"{title}\" was added to your library." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    private List<object> ExtractTopN(string json, bool isMovie, int count)
    {
        var list = new List<object>();
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");

        int limit = Math.Min(count, results.GetArrayLength());
        for (int i = 0; i < limit; i++)
        {
            var item = results[i];
            var posterPath = item.TryGetProperty("poster_path", out var p) ? p.GetString() : null;

            list.Add(new
            {
                rank = i + 1,
                title = isMovie
                    ? item.GetProperty("title").GetString()
                    : item.GetProperty("name").GetString(),
                overview = item.GetProperty("overview").GetString(),
                rating = item.TryGetProperty("vote_average", out var v) ? v.GetDouble() : 0,
                poster = posterPath != null ? $"https://image.tmdb.org/t/p/w342{posterPath}" : null,
                releaseDate = isMovie
                    ? (item.TryGetProperty("release_date", out var rd) ? rd.GetString() : null)
                    : (item.TryGetProperty("first_air_date", out var fad) ? fad.GetString() : null),
                tmdbId = item.GetProperty("id").GetInt32(),
                isMovie
            });
        }

        return list;
    }
}