using System.Net.Http.Json;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class OmdbService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OmdbService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Omdb:ApiKey"] ?? "";
    }

    public async Task<List<OmdbSearchItem>> SearchAsync(string query)
    {
        var url = $"http://www.omdbapi.com/?apikey={_apiKey}&s={Uri.EscapeDataString(query)}";
        var result = await _http.GetFromJsonAsync<OmdbSearchResponse>(url);
        return result?.Search ?? new List<OmdbSearchItem>();
    }

    public async Task<OmdbDetail?> GetDetailsAsync(string imdbId)
    {
        var url = $"http://www.omdbapi.com/?apikey={_apiKey}&i={imdbId}&plot=full";
        return await _http.GetFromJsonAsync<OmdbDetail>(url);
    }

   public async Task<List<Episode>> FetchEpisodesAsync(string title)
{
    var episodes = new List<Episode>();

    try
    {
        var searchResults = await SearchAsync(title);
        var best = searchResults.FirstOrDefault(r => r.Type == "series") ?? searchResults.FirstOrDefault();
        if (best == null || string.IsNullOrEmpty(best.imdbID)) return episodes;

        var details = await GetDetailsAsync(best.imdbID);
        if (details == null || string.IsNullOrEmpty(details.totalSeasons)) return episodes;

        if (!int.TryParse(details.totalSeasons, out var totalSeasons)) return episodes;

        for (int season = 1; season <= totalSeasons; season++)
        {
            var url = $"http://www.omdbapi.com/?apikey={_apiKey}&i={best.imdbID}&Season={season}";
            var seasonResult = await _http.GetFromJsonAsync<OmdbSeasonResponse>(url);
            if (seasonResult?.Episodes == null) continue;

            foreach (var ep in seasonResult.Episodes)
            {
                int.TryParse(ep.Episode, out var episodeNum);
                DateTime.TryParse(ep.Released, out var airDate);

                episodes.Add(new Episode
                {
                    SeasonNumber = season,
                    EpisodeNumber = episodeNum,
                    Name = ep.Title ?? "Untitled",
                    AirDate = airDate == default ? null : airDate,
                    Watched = false
                });
            }
        }
    }
    catch
    {
        return episodes;
    }

    return episodes;
}
}