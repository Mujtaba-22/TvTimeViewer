using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TvTimeViewer.Data;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class MangaUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MangaUpdateService> _logger;
    private const string AniListUrl = "https://graphql.anilist.co";

    public MangaUpdateService(IServiceScopeFactory scopeFactory, ILogger<MangaUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewChapters(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manga auto-update failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CheckForNewChapters(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = httpFactory.CreateClient();

        var mangaList = await db.Manga
            .Where(m => m.AniListId.HasValue && !m.Completed)
            .ToListAsync(ct);

        if (!mangaList.Any())
        {
            _logger.LogInformation("No active manga titles to check for updates.");
            return;
        }

        int updatedCount = 0;

        foreach (var manga in mangaList)
        {
            try
            {
                var query = @"query { Media(id: " + manga.AniListId + @", type: MANGA) { chapters status } }";
                var payload = JsonSerializer.Serialize(new { query });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await client.PostAsync(AniListUrl,
                    new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AniList returned {StatusCode} for {Title}", response.StatusCode, manga.Title);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var dataEl) ||
                    dataEl.ValueKind == JsonValueKind.Null ||
                    !dataEl.TryGetProperty("Media", out var m) ||
                    m.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                var latestChapters = m.TryGetProperty("chapters", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetInt32()
                    : (int?)null;

                if (latestChapters.HasValue && (!manga.TotalChapters.HasValue || latestChapters.Value > manga.TotalChapters.Value))
                {
                    var existingNumbers = await db.MangaChapters
                        .Where(ch => ch.MangaId == manga.Id)
                        .Select(ch => ch.ChapterNumber)
                        .ToListAsync(ct);

                    var startFrom = existingNumbers.Any() ? existingNumbers.Max() + 1 : 1;

                    for (int i = startFrom; i <= latestChapters.Value; i++)
                    {
                        db.MangaChapters.Add(new MangaChapter
                        {
                            MangaId = manga.Id,
                            ChapterNumber = i,
                            Read = false
                        });
                    }

                    var newCount = latestChapters.Value - startFrom + 1;
                    manga.TotalChapters = latestChapters.Value;
                    updatedCount++;

                    _logger.LogInformation("Found {Count} new chapter(s) for {Title} (now {Total} total).",
                        newCount, manga.Title, latestChapters.Value);
                }

                await Task.Delay(500, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AniList request timed out for {Title}", manga.Title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check updates for manga {Title}", manga.Title);
            }
        }

        if (updatedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Manga update check complete. {Count} title(s) had new chapters.", updatedCount);
        }
        else
        {
            _logger.LogInformation("Manga update check complete. No new chapters found.");
        }
    }
}