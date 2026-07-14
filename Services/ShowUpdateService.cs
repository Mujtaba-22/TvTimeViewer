using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class ShowUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShowUpdateService> _logger;

    public ShowUpdateService(IServiceScopeFactory scopeFactory, ILogger<ShowUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewEpisodes(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Show auto-update failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CheckForNewEpisodes(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        var shows = await db.Shows
            .Include(s => s.Episodes)
            .Where(s => s.Followed && !s.Archived)
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

        var client = httpFactory.CreateClient("tmdb");
        var updates = new List<object>();

        foreach (var show in shows)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
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
                    db.Episodes.AddRange(newEpisodes);

                    bool isNewSeason = maxIncomingSeason > maxExistingSeason && maxExistingSeason > 0;

                    updates.Add(new
                    {
                        showId = show.Id,
                        title = show.Title,
                        newEpisodeCount = newEpisodes.Count,
                        isNewSeason,
                        latestSeason = maxIncomingSeason,
                        latestEpisodeTitle = newEpisodes.OrderByDescending(e => e.SeasonNumber)
                            .ThenByDescending(e => e.EpisodeNumber).First().Title
                    });

                    _logger.LogInformation("Found {Count} new episode(s) for {Title}.", newEpisodes.Count, show.Title);
                }

                await Task.Delay(300, ct);
            }
            catch
            {
                continue;
            }
        }

        if (updates.Any())
            await db.SaveChangesAsync(ct);

        var result = new { checkedAt = DateTime.UtcNow, updates };
        cache.Set("show-updates-result", result, TimeSpan.FromHours(3));
    }
}