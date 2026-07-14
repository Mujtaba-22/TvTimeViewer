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

    public async Task<string?> FindMangaDexIdAsync(string title)
    {
        var client = _httpFactory.CreateClient();
        try
        {
            var url = $"{BaseUrl}/manga?title={Uri.EscapeDataString(title)}&limit=1";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            return data[0].GetProperty("id").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<(double? latestChapterNumber, DateTime? latestPublishAt)> GetLatestChapterAsync(string mangaDexId)
    {
        var client = _httpFactory.CreateClient();
        try
        {
            var url = $"{BaseUrl}/manga/{mangaDexId}/feed?translatedLanguage[]=en&order[chapter]=desc&limit=1&includeFuturePublishAt=0";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (null, null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return (null, null);

            var attrs = data[0].GetProperty("attributes");
            var chapterStr = attrs.TryGetProperty("chapter", out var ch) ? ch.GetString() : null;
            var publishAtStr = attrs.TryGetProperty("publishAt", out var pa) ? pa.GetString() : null;

            double? chapterNum = double.TryParse(chapterStr, out var num) ? num : null;
            DateTime? publishAt = DateTime.TryParse(publishAtStr, out var date) ? date : null;

            return (chapterNum, publishAt);
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<List<(double chapterNumber, DateTime publishAt)>> GetRecentChaptersAsync(string mangaDexId, int limit = 5)
    {
        var results = new List<(double, DateTime)>();
        var client = _httpFactory.CreateClient();

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
        catch { }

        return results;
    }
}