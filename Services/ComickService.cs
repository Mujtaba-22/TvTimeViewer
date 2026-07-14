using System.Text.Json;

namespace TvTimeViewer.Services;

public class ComickService
{
    private readonly IHttpClientFactory _httpFactory;
    private const string BaseUrl = "https://api.comick.dev";

    public ComickService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        return client;
    }

    public async Task<string?> FindComickHidAsync(string title)
    {
        var client = CreateClient();
        try
        {
            var url = $"{BaseUrl}/v1.0/search?q={Uri.EscapeDataString(title)}&limit=1";
            Console.WriteLine($"[Comick] Requesting URL: {url}");

            var response = await client.GetAsync(url);
            Console.WriteLine($"[Comick] Search status for '{title}': {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Comick] Search failed body: {body.Substring(0, Math.Min(300, body.Length))}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                Console.WriteLine($"[Comick] No results for '{title}'.");
                return null;
            }

            var hid = doc.RootElement[0].GetProperty("hid").GetString();
            Console.WriteLine($"[Comick] Found hid for '{title}': {hid}");
            return hid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Comick] Search EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<int?> GetChapterCountAsync(string comickHid)
    {
        var client = CreateClient();
        try
        {
            var url = $"{BaseUrl}/comic/{comickHid}/chapters?lang=en&limit=1";
            Console.WriteLine($"[Comick] Requesting URL: {url}");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Comick] ChapterCount failed for {comickHid}: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            double highestChapter = 0;
            if (doc.RootElement.TryGetProperty("chapters", out var chapters) && chapters.ValueKind == JsonValueKind.Array)
            {
                foreach (var ch in chapters.EnumerateArray())
                {
                    if (ch.TryGetProperty("chap", out var chapEl) && chapEl.ValueKind == JsonValueKind.String
                        && double.TryParse(chapEl.GetString(), out var num) && num > highestChapter)
                        highestChapter = num;
                }
            }

            return highestChapter > 0 ? (int)Math.Floor(highestChapter) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Comick] ChapterCount EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }

    public async Task<List<(double chapterNumber, DateTime publishAt)>> GetRecentChaptersAsync(string comickHid, int limit = 5)
    {
        var results = new List<(double, DateTime)>();
        var client = CreateClient();

        try
        {
            var url = $"{BaseUrl}/comic/{comickHid}/chapters?lang=en&limit={limit}";
            Console.WriteLine($"[Comick] Requesting URL: {url}");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Comick] RecentChapters failed for {comickHid}: {response.StatusCode}");
                return results;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("chapters", out var chapters) || chapters.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var ch in chapters.EnumerateArray())
            {
                var chapStr = ch.TryGetProperty("chap", out var chapEl) ? chapEl.GetString() : null;
                var publishStr = ch.TryGetProperty("publish_at", out var pubEl) ? pubEl.GetString() : null;

                if (double.TryParse(chapStr, out var num) && DateTime.TryParse(publishStr, out var date))
                    results.Add((num, date));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Comick] RecentChapters EXCEPTION: {ex.GetType().Name} - {ex.Message}");
        }

        return results;
    }
}