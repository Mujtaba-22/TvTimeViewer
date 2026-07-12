using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Controllers;

public class UpdatesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;

    public UpdatesController(AppDbContext db, IHttpClientFactory httpFactory, IMemoryCache cache)
    {
        _db = db;
        _httpFactory = httpFactory;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Check(bool force = false)
    {
        const string cacheKey = "show-updates-result";

        if (!force && _cache.TryGetValue(cacheKey, out object? cached))
            return Json(cached!);

        var shows = await _db.Shows
            .Include(s => s.Episodes)
            .Where(s => s.Followed && !s.Archived)
            .OrderBy(s => s.Title)
            .ToListAsync();

        var client = _httpFactory.CreateClient("tmdb");
        var updates = new List<object>();

        foreach (var show in shows)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));

                var searchUrl = $"https://api.themoviedb.org/3/search/tv?query={Uri.EscapeDataString(show.Title)}";
                var tvmazeUrl = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(show.Title)}&embed=episodes";

                var response = await client.GetAsync(tvmazeUrl, cts.Token);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("_embedded", out var embedded) ||
                    !embedded.TryGetProperty("episodes", out var episodesEl))
                    continue;

                var existingKeys = show.Episodes
                    .Select(e => (e.SeasonNumber, e.EpisodeNumber))
                    .ToHashSet();

                var newEpisodes = new List<Episode>();
                var maxExistingSeason = show.Episodes.Any() ? show.Episodes.Max(e => e.SeasonNumber) : 0;
                int maxIncomingSeason = 0;

                foreach (var ep in episodesEl.EnumerateArray())
                {
                    var season = ep.GetProperty("season").GetInt32();
                    var number = ep.GetProperty("number").GetInt32();
                    maxIncomingSeason = Math.Max(maxIncomingSeason, season);

                    if (existingKeys.Contains((season, number))) continue;

                    var airDateStr = ep.TryGetProperty("airdate", out var ad) ? ad.GetString() : null;
                    DateTime? airDate = DateTime.TryParse(airDateStr, out var parsed) ? parsed : null;

                    newEpisodes.Add(new Episode
                    {
                        ShowId = show.Id,
                        SeasonNumber = season,
                        EpisodeNumber = number,
                        Title = ep.TryGetProperty("name", out var n) ? n.GetString() ?? $"Episode {number}" : $"Episode {number}",
                        AirDate = airDate,
                        Watched = false
                    });
                }

                if (newEpisodes.Any())
                {
                    _db.Episodes.AddRange(newEpisodes);

                    bool isNewSeason = maxIncomingSeason > maxExistingSeason && maxExistingSeason > 0;

                    updates.Add(new
                    {
                        showId = show.Id,
                        title = show.Title,
                        poster = show.PosterImage != null ? Url.Action("Poster", "Show", new { id = show.Id }) : null,
                        newEpisodeCount = newEpisodes.Count,
                        isNewSeason,
                        latestSeason = maxIncomingSeason,
                        latestEpisodeTitle = newEpisodes.OrderByDescending(e => e.SeasonNumber)
                            .ThenByDescending(e => e.EpisodeNumber).First().Title
                    });
                }
            }
            catch
            {
                continue;
            }
        }

        if (updates.Any())
            await _db.SaveChangesAsync();

        var result = new { checkedAt = DateTime.UtcNow, updates };
        _cache.Set(cacheKey, result, TimeSpan.FromHours(3));

        return Json(result);
    }
}