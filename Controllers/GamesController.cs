using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class GamesController : Controller
{
    private readonly IgdbService _igdb;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public GamesController(IgdbService igdb, IMemoryCache cache, AppDbContext db, IHttpClientFactory httpFactory)
    {
        _igdb = igdb;
        _cache = cache;
        _db = db;
        _httpFactory = httpFactory;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Search()
    {
        return View(new List<object>());
    }

    private static object MapGame(JsonElement item)
    {
        var title = item.GetProperty("name").GetString();
        string? coverUrl = null;
        if (item.TryGetProperty("cover", out var cover) && cover.TryGetProperty("image_id", out var imgId))
            coverUrl = IgdbService.CoverUrl(imgId.GetString()!);

        var genres = item.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).Where(x => x != null).ToList()
            : new List<string?>();

        var platforms = item.TryGetProperty("platforms", out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).Where(x => x != null).ToList()
            : new List<string?>();

        int? year = null;
        if (item.TryGetProperty("first_release_date", out var frd) && frd.ValueKind == JsonValueKind.Number)
            year = DateTimeOffset.FromUnixTimeSeconds(frd.GetInt64()).Year;

        double? rating = item.TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetDouble() : (double?)null;

        return new
        {
            igdbId = item.GetProperty("id").GetInt32(),
            title,
            poster = coverUrl,
            cover = coverUrl,
            genre = genres.FirstOrDefault(),
            genres,
            platform = platforms.FirstOrDefault(),
            year,
            rating,
            isMovie = false,
            format = "game"
        };
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(new List<object>());

        var safeQuery = q.Replace("\"", "");
        var body = $"search \"{safeQuery}\"; fields name,cover.image_id,genres.name,platforms.name,first_release_date,rating; limit 20;";

        try
        {
            using var doc = await _igdb.QueryAsync("games", body);
            var items = doc.RootElement.EnumerateArray().Select(MapGame).ToList();
            return Json(items);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Genres()
    {
        const string cacheKey = "game-genres";
        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Json(cached!);

        try
        {
            using var doc = await _igdb.QueryAsync("genres", "fields id,name; sort name asc; limit 50;");
            var genres = doc.RootElement.EnumerateArray()
                .Select(g => new { id = g.GetProperty("id").GetInt32(), name = g.GetProperty("name").GetString() })
                .ToList();

            _cache.Set(cacheKey, genres, TimeSpan.FromHours(24));
            return Json(genres);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Discover(int? genreId, int page = 1)
    {
        const int perPage = 20;
        var offset = (page - 1) * perPage;
        var genreFilter = genreId.HasValue && genreId.Value > 0 ? $"where genres = {genreId.Value};" : "";

        var body = $"fields name,cover.image_id,genres.name,platforms.name,first_release_date,rating; sort rating desc; {genreFilter} limit {perPage}; offset {offset};";

        try
        {
            using var doc = await _igdb.QueryAsync("games", body);
            var items = doc.RootElement.EnumerateArray().Select(MapGame).ToList();
            return Json(new { items, page, hasNextPage = items.Count == perPage });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> TopRated()
    {
        var body = "fields name,cover.image_id,genres.name,platforms.name,first_release_date,rating; where rating_count > 100; sort rating desc; limit 12;";

        try
        {
            using var doc = await _igdb.QueryAsync("games", body);
            var games = doc.RootElement.EnumerateArray().Select(MapGame).ToList();
            return Json(new { games });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(int igdbId)
    {
        var body = $"fields name,cover.image_id,genres.name,platforms.name,first_release_date,rating; where id = {igdbId};";

        try
        {
            using var doc = await _igdb.QueryAsync("games", body);
            var m = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (m.ValueKind == JsonValueKind.Undefined)
                return Json(new { success = false, message = "Game not found." });

            var title = m.GetProperty("name").GetString() ?? "Untitled";

            var existing = await _db.Games.FirstOrDefaultAsync(x => x.Title == title);
            if (existing != null)
                return Json(new { success = false, message = $"\"{title}\" is already in your library." });

            string? coverUrl = null;
            if (m.TryGetProperty("cover", out var cover) && cover.TryGetProperty("image_id", out var imgId))
                coverUrl = IgdbService.CoverUrl(imgId.GetString()!);

            byte[]? coverBytes = null;
            if (!string.IsNullOrEmpty(coverUrl))
            {
                var client = _httpFactory.CreateClient();
                coverBytes = await client.GetByteArrayAsync(coverUrl);
            }

            var genre = m.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
                ? string.Join(", ", g.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).Where(x => x != null))
                : null;

            var platform = m.TryGetProperty("platforms", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).FirstOrDefault(x => x != null)
                : null;

            int? year = null;
            if (m.TryGetProperty("first_release_date", out var frd) && frd.ValueKind == JsonValueKind.Number)
                year = DateTimeOffset.FromUnixTimeSeconds(frd.GetInt64()).Year;

            double? rating = m.TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetDouble() : (double?)null;

            var game = new Game
            {
                IgdbId = igdbId,
                Title = title,
                Genre = genre,
                Platform = platform,
                CoverImage = coverBytes,
                CoverUrl = coverUrl,
                Rating = rating,
                ReleaseYear = year,
                Completed = false,
                Playing = false,
                HoursPlayed = 0,
                AddedAt = DateTime.UtcNow
            };

            _db.Games.Add(game);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"\"{title}\" was added to your library." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TogglePlaying(int id, string? returnUrl)
    {
        var game = await _db.Games.FindAsync(id);
        if (game == null) return NotFound();

        game.Playing = !game.Playing;
        if (game.Playing) game.LastPlayedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction("Index", "Library") : Redirect(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCompleted(int id, string? returnUrl)
    {
        var game = await _db.Games.FindAsync(id);
        if (game == null) return NotFound();

        game.Completed = !game.Completed;
        if (game.Completed) game.Playing = false;
        await _db.SaveChangesAsync();

        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction("Index", "Library") : Redirect(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateHours(int id, double hours, string? returnUrl)
    {
        var game = await _db.Games.FindAsync(id);
        if (game == null) return NotFound();

        game.HoursPlayed = hours;
        game.LastPlayedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction("Index", "Library") : Redirect(returnUrl);
    }

    public async Task<IActionResult> Details(int id)
    {
        var game = await _db.Games.FindAsync(id);
        if (game == null) return NotFound();
        return View(game);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var game = await _db.Games.FindAsync(id);
        if (game != null) { _db.Games.Remove(game); await _db.SaveChangesAsync(); }
        return RedirectToAction("Index", "Library");
    }

    [HttpGet]
    public async Task<IActionResult> Cover(int id)
    {
        var game = await _db.Games.FindAsync(id);
        if (game?.CoverImage == null) return NotFound();
        return File(game.CoverImage, game.CoverContentType ?? "image/jpeg");
    }
}