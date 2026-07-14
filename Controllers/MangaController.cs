using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class MangaController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly MangaDexService _mangaDex;
    private readonly ComickService _comick;
    private const string AniListUrl = "https://graphql.anilist.co";

    public MangaController(IHttpClientFactory httpFactory, IMemoryCache cache, AppDbContext db, MangaDexService mangaDex, ComickService comick)
    {
        _httpFactory = httpFactory;
        _cache = cache;
        _db = db;
        _mangaDex = mangaDex;
        _comick = comick;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Search()
    {
        return View(new List<object>());
    }

    public async Task<IActionResult> Details(int id)
    {
        var manga = await _db.Manga
            .Include(m => m.Chapters)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (manga == null) return NotFound();

        if (!manga.Chapters.Any())
        {
            int? chaptersToGenerate = manga.TotalChapters;

            if (!chaptersToGenerate.HasValue || chaptersToGenerate.Value == 0)
            {
                if (string.IsNullOrEmpty(manga.ComickHid))
                {
                    manga.ComickHid = await _comick.FindComickHidAsync(manga.Title);
                    if (!string.IsNullOrEmpty(manga.ComickHid))
                        await _db.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(manga.ComickHid))
                    chaptersToGenerate = await _comick.GetChapterCountAsync(manga.ComickHid);

                if (!chaptersToGenerate.HasValue || chaptersToGenerate.Value == 0)
                {
                    if (string.IsNullOrEmpty(manga.MangaDexId))
                    {
                        manga.MangaDexId = await _mangaDex.FindMangaDexIdAsync(manga.Title);
                        if (!string.IsNullOrEmpty(manga.MangaDexId))
                            await _db.SaveChangesAsync();
                    }

                    if (!string.IsNullOrEmpty(manga.MangaDexId))
                    {
                        chaptersToGenerate = await _mangaDex.GetChapterCountFromAggregateAsync(manga.MangaDexId);

                        if (!chaptersToGenerate.HasValue || chaptersToGenerate.Value == 0)
                        {
                            var (latestChapterNumber, _) = await _mangaDex.GetLatestChapterAsync(manga.MangaDexId);
                            if (latestChapterNumber.HasValue && latestChapterNumber.Value > 0)
                                chaptersToGenerate = (int)Math.Floor(latestChapterNumber.Value);
                        }
                    }
                }
            }

            if (chaptersToGenerate.HasValue && chaptersToGenerate.Value > 0)
            {
                for (int i = 1; i <= chaptersToGenerate.Value; i++)
                    manga.Chapters.Add(new MangaChapter { MangaId = manga.Id, ChapterNumber = i, Read = false, AddedAt = DateTime.UtcNow });

                manga.TotalChapters = chaptersToGenerate.Value;

                await _db.SaveChangesAsync();
            }
        }

        manga.Chapters = manga.Chapters.OrderBy(c => c.ChapterNumber).ToList();

        int? estimatedDaysRemaining = null;
        DateTime? estimatedNextDate = null;
        string estimateSource = "none";

        if (!manga.Completed)
        {
            if (string.IsNullOrEmpty(manga.ComickHid))
            {
                manga.ComickHid = await _comick.FindComickHidAsync(manga.Title);
                if (!string.IsNullOrEmpty(manga.ComickHid))
                    await _db.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(manga.ComickHid))
            {
                var recentChapters = await _comick.GetRecentChaptersAsync(manga.ComickHid, 5);

                if (recentChapters.Count >= 2)
                {
                    var ordered = recentChapters.OrderBy(c => c.publishAt).ToList();
                    var gaps = new List<double>();

                    for (int i = 1; i < ordered.Count; i++)
                        gaps.Add((ordered[i].publishAt - ordered[i - 1].publishAt).TotalDays);

                    if (gaps.Any(g => g > 0.1))
                    {
                        var avgGap = gaps.Where(g => g > 0.1).Average();
                        var lastPublish = ordered.Max(c => c.publishAt);
                        estimatedNextDate = lastPublish.AddDays(avgGap);
                        var remaining = (estimatedNextDate.Value - DateTime.UtcNow).TotalDays;
                        estimatedDaysRemaining = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
                        estimateSource = "comick";
                    }
                }
            }

            if (estimateSource == "none")
            {
                if (string.IsNullOrEmpty(manga.MangaDexId))
                {
                    manga.MangaDexId = await _mangaDex.FindMangaDexIdAsync(manga.Title);
                    if (!string.IsNullOrEmpty(manga.MangaDexId))
                        await _db.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(manga.MangaDexId))
                {
                    var recentChapters = await _mangaDex.GetRecentChaptersAsync(manga.MangaDexId, 5);

                    if (recentChapters.Count >= 2)
                    {
                        var ordered = recentChapters.OrderBy(c => c.publishAt).ToList();
                        var gaps = new List<double>();

                        for (int i = 1; i < ordered.Count; i++)
                            gaps.Add((ordered[i].publishAt - ordered[i - 1].publishAt).TotalDays);

                        if (gaps.Any(g => g > 0.1))
                        {
                            var avgGap = gaps.Where(g => g > 0.1).Average();
                            var lastPublish = ordered.Max(c => c.publishAt);
                            estimatedNextDate = lastPublish.AddDays(avgGap);
                            var remaining = (estimatedNextDate.Value - DateTime.UtcNow).TotalDays;
                            estimatedDaysRemaining = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
                            estimateSource = "mangadex";
                        }
                    }
                }
            }

            if (estimateSource == "none" && manga.Chapters.Count >= 2)
            {
                var recent = manga.Chapters.OrderByDescending(c => c.ChapterNumber).Take(5).OrderBy(c => c.AddedAt).ToList();
                var gaps = new List<double>();

                for (int i = 1; i < recent.Count; i++)
                {
                    var gap = (recent[i].AddedAt - recent[i - 1].AddedAt).TotalDays;
                    if (gap > 0.1) gaps.Add(gap);
                }

                if (gaps.Any())
                {
                    var avgGap = gaps.Average();
                    var lastAdded = manga.Chapters.Max(c => c.AddedAt);
                    estimatedNextDate = lastAdded.AddDays(avgGap);
                    var remaining = (estimatedNextDate.Value - DateTime.UtcNow).TotalDays;
                    estimatedDaysRemaining = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
                    estimateSource = "local";
                }
            }
        }

        ViewBag.EstimatedDaysRemaining = estimatedDaysRemaining;
        ViewBag.EstimatedNextDate = estimatedNextDate;
        ViewBag.EstimateSource = estimateSource;

        return View(manga);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleChapter(int chapterId)
    {
        var chapter = await _db.MangaChapters.FindAsync(chapterId);
        if (chapter == null) return NotFound();

        chapter.Read = !chapter.Read;
        chapter.ReadAt = chapter.Read ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        await RecalculateProgress(chapter.MangaId);

        return Json(new { success = true, read = chapter.Read });
    }

    [HttpPost]
    public async Task<IActionResult> MarkUpTo(int mangaId, int chapterNumber)
    {
        var chapters = await _db.MangaChapters
            .Where(c => c.MangaId == mangaId && c.ChapterNumber <= chapterNumber)
            .ToListAsync();

        foreach (var c in chapters)
        {
            c.Read = true;
            c.ReadAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await RecalculateProgress(mangaId);

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> AddChapter(int mangaId)
    {
        var manga = await _db.Manga.Include(m => m.Chapters).FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null) return NotFound();

        var nextNumber = manga.Chapters.Any() ? manga.Chapters.Max(c => c.ChapterNumber) + 1 : 1;
        var chapter = new MangaChapter { MangaId = mangaId, ChapterNumber = nextNumber, Read = false, AddedAt = DateTime.UtcNow };
        _db.MangaChapters.Add(chapter);

        if (manga.TotalChapters.HasValue && nextNumber > manga.TotalChapters.Value)
            manga.TotalChapters = nextNumber;

        await _db.SaveChangesAsync();

        return Json(new { success = true, chapterId = chapter.Id, chapterNumber = chapter.ChapterNumber });
    }

    private async Task RecalculateProgress(int mangaId)
    {
        var manga = await _db.Manga.Include(m => m.Chapters).FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null) return;

        manga.ChaptersRead = manga.Chapters.Count(c => c.Read);
        manga.LastReadAt = manga.Chapters.Any(c => c.Read) ? DateTime.UtcNow : manga.LastReadAt;

        if (manga.TotalChapters.HasValue && manga.ChaptersRead >= manga.TotalChapters.Value && manga.TotalChapters.Value > 0)
            manga.Completed = true;
        else
            manga.Completed = false;

        await _db.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Genres()
    {
        const string cacheKey = "manga-genres";
        if (_cache.TryGetValue(cacheKey, out object? cached))
            return Json(cached!);

        const string query = @"query { GenreCollection }";
        var payload = JsonSerializer.Serialize(new { query });
        var client = _httpFactory.CreateClient();

        try
        {
            var response = await client.PostAsync(AniListUrl,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var genres = doc.RootElement.GetProperty("data").GetProperty("GenreCollection")
                .EnumerateArray().Select(g => g.GetString()).ToList();

            _cache.Set(cacheKey, genres, TimeSpan.FromHours(24));
            return Json(genres);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(new List<object>());

        var query = @"
        query {
          Page(page: 1, perPage: 20) {
            media(type: MANGA, search: """ + q.Replace("\"", "") + @""", sort: POPULARITY_DESC) {
              id
              title { romaji english }
              coverImage { large }
              format
              countryOfOrigin
              averageScore
              startDate { year }
            }
          }
        }";

        var payload = JsonSerializer.Serialize(new { query });
        var client = _httpFactory.CreateClient();

        try
        {
            var response = await client.PostAsync(AniListUrl,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var media = doc.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media");

            var items = new List<object>();
            foreach (var item in media.EnumerateArray())
            {
                var titleEl = item.GetProperty("title");
                var title = titleEl.TryGetProperty("english", out var en) && en.ValueKind == JsonValueKind.String
                    ? en.GetString()
                    : titleEl.GetProperty("romaji").GetString();

                var country = item.TryGetProperty("countryOfOrigin", out var co) ? co.GetString() : "JP";
                var formatLabel = country switch { "KR" => "Manhwa", "CN" => "Manhua", _ => "Manga" };

                items.Add(new
                {
                    aniListId = item.GetProperty("id").GetInt32(),
                    title,
                    poster = item.GetProperty("coverImage").GetProperty("large").GetString(),
                    year = item.TryGetProperty("startDate", out var sd) && sd.TryGetProperty("year", out var yr) && yr.ValueKind == JsonValueKind.Number ? yr.GetInt32().ToString() : "",
                    type = formatLabel,
                    format = formatLabel.ToLower()
                });
            }

            return Json(items);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> TopRated()
    {
        async Task<List<object>> FetchTop(string country)
        {
            var query = @"
            query {
              Page(page: 1, perPage: 12) {
                media(type: MANGA, countryOfOrigin: """ + country + @""", sort: SCORE_DESC) {
                  id
                  title { romaji english }
                  coverImage { large }
                  averageScore
                  startDate { year }
                }
              }
            }";

            var payload = JsonSerializer.Serialize(new { query });
            var client = _httpFactory.CreateClient();
            var response = await client.PostAsync(AniListUrl,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var media = doc.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media");

            var items = new List<object>();
            foreach (var item in media.EnumerateArray())
            {
                var titleEl = item.GetProperty("title");
                var title = titleEl.TryGetProperty("english", out var en) && en.ValueKind == JsonValueKind.String
                    ? en.GetString()
                    : titleEl.GetProperty("romaji").GetString();

                items.Add(new
                {
                    aniListId = item.GetProperty("id").GetInt32(),
                    title,
                    poster = item.GetProperty("coverImage").GetProperty("large").GetString(),
                    rating = item.TryGetProperty("averageScore", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() / 10.0 : 0,
                    releaseDate = item.TryGetProperty("startDate", out var sd) && sd.TryGetProperty("year", out var yr) && yr.ValueKind == JsonValueKind.Number ? yr.GetInt32().ToString() : "",
                    isMovie = false,
                    format = country == "KR" ? "manhwa" : "manga"
                });
            }

            return items;
        }

        try
        {
            var manga = await FetchTop("JP");
            var manhwa = await FetchTop("KR");
            return Json(new { manga, manhwa });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Discover(string? format, string? genre, int page = 1)
    {
        var countryOfOrigin = format switch
        {
            "manhwa" => "KR",
            "manhua" => "CN",
            _ => "JP"
        };

        var genreFilter = string.IsNullOrWhiteSpace(genre) ? "" : $", genre: \"{genre}\"";

        var query = @"
        query {
          Page(page: " + page + @", perPage: 20) {
            pageInfo { currentPage hasNextPage }
            media(type: MANGA, countryOfOrigin: """ + countryOfOrigin + @"""" + genreFilter + @", sort: POPULARITY_DESC) {
              id
              title { romaji english }
              coverImage { large }
              genres
              averageScore
              chapters
              status
              startDate { year }
            }
          }
        }";

        var payload = JsonSerializer.Serialize(new { query });
        var client = _httpFactory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            var response = await client.PostAsync(AniListUrl,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), cts.Token);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var page_ = doc.RootElement.GetProperty("data").GetProperty("Page");
            var hasNextPage = page_.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean();
            var media = page_.GetProperty("media");

            var items = new List<object>();
            foreach (var item in media.EnumerateArray())
            {
                var titleEl = item.GetProperty("title");
                var title = titleEl.TryGetProperty("english", out var en) && en.ValueKind == JsonValueKind.String
                    ? en.GetString()
                    : titleEl.GetProperty("romaji").GetString();

                items.Add(new
                {
                    aniListId = item.GetProperty("id").GetInt32(),
                    title,
                    cover = item.GetProperty("coverImage").GetProperty("large").GetString(),
                    genres = item.GetProperty("genres").EnumerateArray().Select(g => g.GetString()).ToList(),
                    rating = item.TryGetProperty("averageScore", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0,
                    chapters = item.TryGetProperty("chapters", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : (int?)null,
                    status = item.TryGetProperty("status", out var st) ? st.GetString() : null,
                    year = item.TryGetProperty("startDate", out var sd) && sd.TryGetProperty("year", out var yr) && yr.ValueKind == JsonValueKind.Number ? yr.GetInt32() : (int?)null,
                    format
                });
            }

            return Json(new { items, page, hasNextPage });
        }
        catch (OperationCanceledException)
        {
            return Json(new { error = "AniList took too long to respond." });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(int aniListId, string format)
    {
        var query = @"
        query {
          Media(id: " + aniListId + @", type: MANGA) {
            id
            title { romaji english }
            coverImage { large }
            genres
            chapters
          }
        }";

        var payload = JsonSerializer.Serialize(new { query });
        var client = _httpFactory.CreateClient();

        try
        {
            var response = await client.PostAsync(AniListUrl,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var m = doc.RootElement.GetProperty("data").GetProperty("Media");

            var titleEl = m.GetProperty("title");
            var title = titleEl.TryGetProperty("english", out var en) && en.ValueKind == JsonValueKind.String
                ? en.GetString() ?? "Untitled"
                : titleEl.GetProperty("romaji").GetString() ?? "Untitled";

            var existing = await _db.Manga.FirstOrDefaultAsync(x => x.Title == title);
            if (existing != null)
                return Json(new { success = false, message = $"\"{title}\" is already in your library." });

            var genres = m.TryGetProperty("genres", out var genresEl)
                ? string.Join(", ", genresEl.EnumerateArray().Select(g => g.GetString()))
                : null;

            var coverUrl = m.GetProperty("coverImage").GetProperty("large").GetString();
            byte[]? coverBytes = null;
            if (!string.IsNullOrEmpty(coverUrl))
                coverBytes = await client.GetByteArrayAsync(coverUrl);

            var totalChapters = m.TryGetProperty("chapters", out var ch) && ch.ValueKind == JsonValueKind.Number ? ch.GetInt32() : (int?)null;

            var manga = new Manga
            {
                AniListId = aniListId,
                Title = title,
                Format = format switch { "manhwa" => "Manhwa", "manhua" => "Manhua", _ => "Manga" },
                Genre = genres,
                CoverImage = coverBytes,
                CoverUrl = coverUrl,
                TotalChapters = totalChapters,
                Following = false,
                Completed = false
            };

            _db.Manga.Add(manga);
            await _db.SaveChangesAsync();

            if (totalChapters.HasValue && totalChapters.Value > 0)
            {
                for (int i = 1; i <= totalChapters.Value; i++)
                    _db.MangaChapters.Add(new MangaChapter { MangaId = manga.Id, ChapterNumber = i, Read = false, AddedAt = DateTime.UtcNow });

                await _db.SaveChangesAsync();
            }

            return Json(new { success = true, message = $"\"{title}\" was added to your library." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProgress(int id, int chaptersRead)
    {
        var manga = await _db.Manga.FindAsync(id);
        if (manga == null) return NotFound();

        manga.ChaptersRead = chaptersRead;
        manga.LastReadAt = DateTime.UtcNow;
        if (manga.TotalChapters.HasValue && chaptersRead >= manga.TotalChapters.Value)
            manga.Completed = true;

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var manga = await _db.Manga.FindAsync(id);
        if (manga != null) { _db.Manga.Remove(manga); await _db.SaveChangesAsync(); }
        return RedirectToAction("Index", "Library");
    }

    [HttpGet]
    public async Task<IActionResult> Cover(int id)
    {
        var manga = await _db.Manga.FindAsync(id);
        if (manga?.CoverImage == null) return NotFound();
        return File(manga.CoverImage, manga.CoverContentType ?? "image/jpeg");
    }
}