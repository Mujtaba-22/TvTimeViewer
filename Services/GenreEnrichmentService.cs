using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;

namespace TvTimeViewer.Services;

public class GenreEnrichmentService
{
    private readonly AppDbContext _db;
    private readonly OmdbService _omdb;

    public GenreEnrichmentService(AppDbContext db, OmdbService omdb)
    {
        _db = db;
        _omdb = omdb;
    }

    public async Task<(int showsUpdated, int moviesUpdated)> EnrichGenresAsync(Action<int, int>? onProgress = null)
    {
        var shows = await _db.Shows.Where(s => string.IsNullOrEmpty(s.Genre)).ToListAsync();
        var movies = await _db.Movies.Where(m => string.IsNullOrEmpty(m.Genre)).ToListAsync();
        int total = shows.Count + movies.Count;
        int current = 0;

        int showsUpdated = 0;
        foreach (var show in shows)
        {
            try
            {
                var results = await _omdb.SearchAsync(show.Title);
                var best = results.FirstOrDefault(r => r.Type == "series") ?? results.FirstOrDefault();
                if (best != null)
                {
                    var detail = await _omdb.GetDetailsAsync(best.imdbID);
                    if (detail != null && !string.IsNullOrEmpty(detail.Genre) && detail.Genre != "N/A")
                    {
                        show.Genre = detail.Genre;
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
                if (best != null)
                {
                    var detail = await _omdb.GetDetailsAsync(best.imdbID);
                    if (detail != null && !string.IsNullOrEmpty(detail.Genre) && detail.Genre != "N/A")
                    {
                        movie.Genre = detail.Genre;
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

    public async Task<List<string>> GetDistinctShowGenresAsync()
    {
        var genreStrings = await _db.Shows
            .Where(s => !string.IsNullOrEmpty(s.Genre))
            .Select(s => s.Genre!)
            .ToListAsync();

        return genreStrings
            .SelectMany(g => g.Split(',', StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }

    public async Task<List<string>> GetDistinctMovieGenresAsync()
    {
        var genreStrings = await _db.Movies
            .Where(m => !string.IsNullOrEmpty(m.Genre))
            .Select(m => m.Genre!)
            .ToListAsync();

        return genreStrings
            .SelectMany(g => g.Split(',', StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }
}