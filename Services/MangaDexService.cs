using System.Text.Json;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class MangaDexService
{
    private readonly IHttpClientFactory _httpFactory;
    private const string BaseUrl = "https://api.mangadex.org";

    public MangaDexService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
            client.DefaultRequestHeaders.Add("User-Agent",
                "TvTimeViewer/1.0 (personal manga tracker; contact: youremail@example.com)");
        return client;
    }

    public async Task<string?> FindMangaDexIdAsync(string title)
    {
        var client = CreateClient();
        try
        {
            var url = $"{BaseUrl}/manga?title={Uri.EscapeDataString(title)}&limit=1";
            Console.WriteLine($"[MangaDex] Requesting URL: {url}");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[MangaDex] Search failed for '{title}': {response.StatusCode}");
                Console.WriteLine($"[MangaDex] Error body: {errorBody}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0)
            {
                Console.WriteLine($"[MangaDex] No search results for '{title}'.");
                return null;
            }

            var foundId = data[0].GetProperty("id").GetString();
            Console.WriteLine($"[MangaDex] Found ID for '{title}': {foundId}");
            return foundId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MangaDex] Search EXCEPTION for '{title}': {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<int?> GetChapterCountFromAggregateAsync(string mangaDexId)
    {
        var client = CreateClient();
        try
        {
            var url = $"{BaseUrl}/manga/{mangaDexId}/aggregate";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[MangaDex] Aggregate failed for {mangaDexId}: {response.StatusCode}");
                Console.WriteLine($"[MangaDex] Error body: {errorBody}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("volumes", out var volumes) || volumes.ValueKind != JsonValueKind.Object)
                return null;

            double highestChapter = 0;

            foreach (var volume in volumes.EnumerateObject())
            {
                if (!volume.Value.TryGetProperty("chapters", out var chapters) || chapters.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var chapter in chapters.EnumerateObject())
                {
                    if (double.TryParse(chapter.Name, out var chNum) && chNum > highestChapter)
                        highestChapter = chNum;
                }
            }

            return highestChapter > 0 ? (int)Math.Floor(highestChapter) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MangaDex] Aggregate EXCEPTION for {mangaDexId}: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<(double? latestChapterNumber, DateTime? latestPublishAt)> GetLatestChapterAsync(string mangaDexId)
    {
        var client = CreateClient();
        try
        {
            var url = $"{BaseUrl}/manga/{mangaDexId}/feed?translatedLanguage[]=en&order[chapter]=desc&limit=20&includeFuturePublishAt=0";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (null, null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return (null, null);

            double? bestChapter = null;
            DateTime? bestPublishAt = null;

            foreach (var item in data.EnumerateArray())
            {
                var attrs = item.GetProperty("attributes");
                var chapterStr = attrs.TryGetProperty("chapter", out var ch) ? ch.GetString() : null;
                var publishAtStr = attrs.TryGetProperty("publishAt", out var pa) ? pa.GetString() : null;

                if (double.TryParse(chapterStr, out var num) && DateTime.TryParse(publishAtStr, out var date))
                {
                    if (!bestChapter.HasValue || num > bestChapter.Value)
                    {
                        bestChapter = num;
                        bestPublishAt = date;
                    }
                }
            }

            return (bestChapter, bestPublishAt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MangaDex] Feed EXCEPTION for {mangaDexId}: {ex.GetType().Name} - {ex.Message}");
            return (null, null);
        }
    }

    public async Task<List<(double chapterNumber, DateTime publishAt)>> GetRecentChaptersAsync(string mangaDexId, int limit = 5)
    {
        var results = new List<(double, DateTime)>();
        var client = CreateClient();

        try
        {
            var url = $"{BaseUrl}/manga/{mangaDexId}/feed?translatedLanguage[]=en&order[chapter]=desc&limit={limit}&includeFuturePublishAt=0";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");

            foreach (var item in data.EnumerateArray())
            {
                var attrs = item.GetProperty("attributes");
                var chapterStr = attrs.TryGetProperty("chapter", out var ch) ? ch.GetString() : null;
                var publishAtStr = attrs.TryGetProperty("publishAt", out var pa) ? pa.GetString() : null;

                if (double.TryParse(chapterStr, out var num) && DateTime.TryParse(publishAtStr, out var date))
                    results.Add((num, date));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MangaDex] RecentChapters EXCEPTION for {mangaDexId}: {ex.GetType().Name} - {ex.Message}");
        }

        return results;
    }
}