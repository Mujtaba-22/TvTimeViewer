using System.Text.Json;

namespace TvTimeViewer.Services;

public class IgdbService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public IgdbService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiresAt)
            return _accessToken;

        var clientId = _config["Igdb:ClientId"];
        var clientSecret = _config["Igdb:ClientSecret"];
        var client = _httpFactory.CreateClient();

        var url = $"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials";
        var response = await client.PostAsync(url, null);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        return _accessToken!;
    }

    public async Task<JsonDocument> QueryAsync(string endpoint, string apicalypseBody)
    {
        var token = await GetAccessTokenAsync();
        var clientId = _config["Igdb:ClientId"];
        var client = _httpFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}")
        {
            Content = new StringContent(apicalypseBody)
        };
        request.Headers.Add("Client-ID", clientId);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    public static string CoverUrl(string imageId, string size = "cover_big") =>
        $"https://images.igdb.com/igdb/image/upload/t_{size}/{imageId}.jpg";
}