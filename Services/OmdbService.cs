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
}