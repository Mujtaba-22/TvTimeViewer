using System.Text.Json;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class TvmazeService
{
    private readonly IHttpClientFactory _httpFactory;

    public TvmazeService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<List<Episode>> FetchEpisodesAsync(string title)
    {
        var episodes = new List<Episode>();
        var client = _httpFactory.CreateClient("tmdb");

        try
        {
            var searchUrl = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(title)}&embed=episodes";
            var response = await client.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return episodes;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("_embedded", out var embedded) ||
                !embedded.TryGetProperty("episodes", out var episodesEl))
                return episodes;

            foreach (var ep in episodesEl.EnumerateArray())
            {
                var airDateStr = ep.TryGetProperty("airdate", out var ad) ? ad.GetString() : null;

                episodes.Add(new Episode
                {
                    SeasonNumber = ep.TryGetProperty("season", out var s) ? s.GetInt32() : 0,
                    EpisodeNumber = ep.TryGetProperty("number", out var num) ? num.GetInt32() : 0,
                    Name = ep.TryGetProperty("name", out var n) ? n.GetString() ?? "Untitled" : "Untitled",
                    AirDate = DateTime.TryParse(airDateStr, out var parsedDate) ? parsedDate : null,
                    Watched = false
                });
            }
        }
        catch
        {
            return episodes;
        }

        return episodes;
    }

    public async Task<string?> FetchPosterAsync(string title)
    {
        var client = _httpFactory.CreateClient("tmdb");

        try
        {
            var searchUrl = $"https://api.tvmaze.com/singlesearch/shows?q={Uri.EscapeDataString(title)}";
            var response = await client.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("image", out var imageEl) || imageEl.ValueKind == JsonValueKind.Null)
                return null;

            var posterUrl = imageEl.TryGetProperty("original", out var orig) && orig.ValueKind == JsonValueKind.String
                ? orig.GetString()
                : imageEl.TryGetProperty("medium", out var med) && med.ValueKind == JsonValueKind.String
                    ? med.GetString()
                    : null;

            return posterUrl;
        }
        catch
        {
            return null;
        }
    }
}