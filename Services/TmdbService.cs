using System.Net.Http.Json;
using TvTimeViewer.Models;

namespace TvTimeViewer.Services;

public class TmdbService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public TmdbService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Tmdb:ApiKey"] ?? "";
    }

    public async Task<List<OmdbSearchItem>> SearchAsync(string query, string type = "all")
    {
        var results = new List<OmdbSearchItem>();
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrWhiteSpace(query))
            return results;

        try
        {
            if (type == "all" || type == "tv")
            {
                var tvUrl = $"https://api.themoviedb.org/3/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";
                var tvResponse = await _http.GetFromJsonAsync<TmdbSearchResponse<TmdbTvSearchResult>>(tvUrl);

                if (tvResponse?.Results != null)
                {
                    foreach (var tv in tvResponse.Results.Take(10))
                    {
                        results.Add(new OmdbSearchItem
                        {
                            Title = tv.Name ?? tv.Original_Name ?? "Untitled",
                            Year = tv.First_Air_Date?.Length >= 4 ? tv.First_Air_Date[..4] : "",
                            imdbID = $"tmdb-{tv.Id}",
                            Type = "series",
                            Poster = string.IsNullOrEmpty(tv.Poster_Path)
                                ? "N/A"
                                : $"https://image.tmdb.org/t/p/w342{tv.Poster_Path}"
                        });
                    }
                }
            }

            if (type == "all" || type == "movie")
            {
                var movieUrl = $"https://api.themoviedb.org/3/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";
                var movieResponse = await _http.GetFromJsonAsync<TmdbSearchResponse<TmdbMovieSearchResult>>(movieUrl);

                if (movieResponse?.Results != null)
                {
                    foreach (var movie in movieResponse.Results.Take(10))
                    {
                        results.Add(new OmdbSearchItem
                        {
                            Title = movie.Title ?? movie.Original_Title ?? "Untitled",
                            Year = movie.Release_Date?.Length >= 4 ? movie.Release_Date[..4] : "",
                            imdbID = $"tmdb-{movie.Id}",
                            Type = "movie",
                            Poster = string.IsNullOrEmpty(movie.Poster_Path)
                                ? "N/A"
                                : $"https://image.tmdb.org/t/p/w342{movie.Poster_Path}"
                        });
                    }
                }
            }
        }
        catch
        {
            return results;
        }

        return results;
    }

    public async Task<List<Episode>> FetchEpisodesByIdAsync(int tmdbId)
    {
        var episodes = new List<Episode>();
        if (string.IsNullOrEmpty(_apiKey))
            return episodes;

        try
        {
            var detailsUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={_apiKey}";
            var details = await _http.GetFromJsonAsync<TmdbTvDetails>(detailsUrl);
            if (details == null || details.Number_Of_Seasons <= 0)
                return episodes;

            for (int season = 1; season <= details.Number_Of_Seasons; season++)
            {
                var seasonUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{season}?api_key={_apiKey}";
                var seasonResult = await _http.GetFromJsonAsync<TmdbSeasonResponse>(seasonUrl);
                if (seasonResult?.Episodes == null)
                    continue;

                foreach (var ep in seasonResult.Episodes)
                {
                    DateTime.TryParse(ep.Air_Date, out var airDate);

                    episodes.Add(new Episode
                    {
                        SeasonNumber = season,
                        EpisodeNumber = ep.Episode_Number,
                        Name = ep.Name ?? "Untitled",
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

    public async Task<List<Episode>> FetchEpisodesAsync(string title)
    {
        try
        {
            var searchUrl = $"https://api.themoviedb.org/3/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(title)}";
            var searchResponse = await _http.GetFromJsonAsync<TmdbSearchResponse<TmdbTvSearchResult>>(searchUrl);
            var best = searchResponse?.Results?.FirstOrDefault();
            if (best == null)
                return new List<Episode>();

            return await FetchEpisodesByIdAsync(best.Id);
        }
        catch
        {
            return new List<Episode>();
        }
    }

    public async Task<OmdbDetail?> GetDetailsAsync(int tmdbId, string type)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        try
        {
            var endpoint = type.Equals("series", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
            var url = $"https://api.themoviedb.org/3/{endpoint}/{tmdbId}?api_key={_apiKey}&append_to_response=credits";
            var json = await _http.GetStringAsync(url);

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = endpoint == "tv"
                ? root.GetProperty("name").GetString() ?? "Untitled"
                : root.GetProperty("title").GetString() ?? "Untitled";

            var dateField = endpoint == "tv" ? "first_air_date" : "release_date";
            var dateStr = root.TryGetProperty(dateField, out var d) ? d.GetString() : null;
            var year = !string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4 ? dateStr[..4] : "";

            var posterPath = root.TryGetProperty("poster_path", out var p) &&
                             p.ValueKind == System.Text.Json.JsonValueKind.String
                ? p.GetString()
                : null;

            var poster = string.IsNullOrEmpty(posterPath)
                ? "N/A"
                : $"https://image.tmdb.org/t/p/w500{posterPath}";

            var genre = "";
            if (root.TryGetProperty("genres", out var genresEl) &&
                genresEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                genre = string.Join(", ", genresEl.EnumerateArray()
                    .Select(g => g.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(n => !string.IsNullOrEmpty(n)));
            }

            var runtime = "";
            if (endpoint == "movie" &&
                root.TryGetProperty("runtime", out var rt) &&
                rt.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                runtime = $"{rt.GetInt32()} min";
            }
            else if (endpoint == "tv" &&
                     root.TryGetProperty("episode_run_time", out var ert) &&
                     ert.ValueKind == System.Text.Json.JsonValueKind.Array &&
                     ert.GetArrayLength() > 0)
            {
                runtime = $"{ert[0].GetInt32()} min";
            }

            var plot = root.TryGetProperty("overview", out var ov) ? ov.GetString() ?? "" : "";
            var rating = root.TryGetProperty("vote_average", out var va) ? va.GetDouble().ToString("0.0") : "N/A";
            var votes = root.TryGetProperty("vote_count", out var vc) ? vc.GetInt32().ToString() : "0";

            var director = "";
            var actors = "";

            if (root.TryGetProperty("credits", out var credits))
            {
                if (credits.TryGetProperty("crew", out var crew) &&
                    crew.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    director = string.Join(", ", crew.EnumerateArray()
                        .Where(c => c.TryGetProperty("job", out var job) && job.GetString() == "Director")
                        .Select(c => c.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Take(3));
                }

                if (credits.TryGetProperty("cast", out var cast) &&
                    cast.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    actors = string.Join(", ", cast.EnumerateArray()
                        .Take(5)
                        .Select(c => c.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(n => !string.IsNullOrEmpty(n)));
                }
            }

            return new OmdbDetail
            {
                Title = title,
                Year = year,
                Genre = genre,
                Plot = plot,
                Poster = poster,
                imdbRating = rating,
                imdbVotes = votes,
                imdbID = $"tmdb-{tmdbId}",
                Type = type,
                Actors = actors,
                Director = director,
                Runtime = runtime
            };
        }
        catch
        {
            return null;
        }
    }
}