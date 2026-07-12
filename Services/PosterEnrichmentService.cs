using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;

namespace TvTimeViewer.Services;

public class PosterEnrichmentService
{
    private readonly AppDbContext _db;
    private readonly TvmazeService _tvmaze;
    private readonly OmdbService _omdb;
    private readonly HttpClient _http;

    public PosterEnrichmentService(AppDbContext db, TvmazeService tvmaze, OmdbService omdb, HttpClient http)
    {
        _db = db;
        _tvmaze = tvmaze;
        _omdb = omdb;
        _http = http;
    }

    public async Task<(int showsUpdated, int moviesUpdated)> EnrichAllAsync(Action<int, int>? onProgress = null)
    {
        var shows = await _db.Shows.Where(s => s.PosterImage == null).ToListAsync();
        var movies = await _db.Movies.Where(m => m.PosterImage == null).ToListAsync();
        int total = shows.Count + movies.Count;
        int current = 0;

        int showsUpdated = 0;
        foreach (var show in shows)
        {
            try
            {
                var posterUrl = await _tvmaze.FetchPosterAsync(show.Title);
                if (!string.IsNullOrEmpty(posterUrl))
                {
                    var (bytes, contentType) = await DownloadImageAsync(posterUrl);
                    if (bytes != null)
                    {
                        show.PosterUrl = posterUrl;
                        show.PosterImage = bytes;
                        show.PosterContentType = contentType;
                        showsUpdated++;
                    }
                }
            }
            catch { }
            current++;
            onProgress?.Invoke(current, total);
            await Task.Delay(250);
        }
        if (showsUpdated > 0) await _db.SaveChangesAsync();

        int moviesUpdated = 0;
        foreach (var movie in movies)
        {
            try
            {
                var results = await _omdb.SearchAsync(movie.Title);
                var best = results.FirstOrDefault(r => r.Type == "movie") ?? results.FirstOrDefault();
                if (best != null && best.Poster != "N/A" && !string.IsNullOrEmpty(best.Poster))
                {
                    var (bytes, contentType) = await DownloadImageAsync(best.Poster);
                    if (bytes != null)
                    {
                        movie.PosterUrl = best.Poster;
                        movie.PosterImage = bytes;
                        movie.PosterContentType = contentType;
                        moviesUpdated++;
                    }
                }
            }
            catch { }
            current++;
            onProgress?.Invoke(current, total);
            await Task.Delay(250);
        }
        if (moviesUpdated > 0) await _db.SaveChangesAsync();

        return (showsUpdated, moviesUpdated);
    }

    private async Task<(byte[]? bytes, string? contentType)> DownloadImageAsync(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (null, null);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (bytes, contentType);
        }
        catch
        {
            return (null, null);
        }
    }
}