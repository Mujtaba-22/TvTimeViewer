using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;

namespace TvTimeViewer.Services;

public class DeduplicationService
{
    private readonly AppDbContext _db;
    public DeduplicationService(AppDbContext db) => _db = db;

    public async Task<(int showsRemoved, int moviesRemoved)> RemoveDuplicatesAsync()
    {
        int showsRemoved = await DeduplicateShowsAsync();
        int moviesRemoved = await DeduplicateMoviesAsync();
        return (showsRemoved, moviesRemoved);
    }

    private async Task<int> DeduplicateShowsAsync()
    {
        var shows = await _db.Shows.Include(s => s.Episodes).ToListAsync();

        var groups = shows
            .GroupBy(s => NormalizeTitle(s.Title))
            .Where(g => g.Count() > 1);

        int removed = 0;

        foreach (var group in groups)
        {
            var ordered = group
                .OrderByDescending(s => s.Episodes.Count)
                .ThenByDescending(s => s.Followed)
                .ThenBy(s => s.Id)
                .ToList();

            var keeper = ordered.First();
            var duplicates = ordered.Skip(1).ToList();

            foreach (var dup in duplicates)
            {
                if (string.IsNullOrEmpty(keeper.PosterUrl) && !string.IsNullOrEmpty(dup.PosterUrl))
                    keeper.PosterUrl = dup.PosterUrl;

                if (dup.LastWatchedAt.HasValue &&
                    (!keeper.LastWatchedAt.HasValue || dup.LastWatchedAt > keeper.LastWatchedAt))
                    keeper.LastWatchedAt = dup.LastWatchedAt;

                keeper.Followed = keeper.Followed || dup.Followed;
                keeper.Archived = keeper.Archived && dup.Archived;

                if (!keeper.Episodes.Any() && dup.Episodes.Any())
                {
                    foreach (var ep in dup.Episodes)
                        ep.ShowId = keeper.Id;
                }
                else
                {
                    _db.Episodes.RemoveRange(dup.Episodes);
                }

                _db.Shows.Remove(dup);
                removed++;
            }
        }

        await _db.SaveChangesAsync();
        return removed;
    }

    private async Task<int> DeduplicateMoviesAsync()
    {
        var movies = await _db.Movies.ToListAsync();

        var groups = movies
            .GroupBy(m => NormalizeTitle(m.Title))
            .Where(g => g.Count() > 1);

        int removed = 0;

        foreach (var group in groups)
        {
            var ordered = group
                .OrderByDescending(m => m.Watched)
                .ThenBy(m => m.Id)
                .ToList();

            var keeper = ordered.First();
            var duplicates = ordered.Skip(1).ToList();

            foreach (var dup in duplicates)
            {
                if (string.IsNullOrEmpty(keeper.PosterUrl) && !string.IsNullOrEmpty(dup.PosterUrl))
                    keeper.PosterUrl = dup.PosterUrl;

                keeper.Watched = keeper.Watched || dup.Watched;

                if (dup.WatchedAt.HasValue &&
                    (!keeper.WatchedAt.HasValue || dup.WatchedAt > keeper.WatchedAt))
                    keeper.WatchedAt = dup.WatchedAt;

                _db.Movies.Remove(dup);
                removed++;
            }
        }

        await _db.SaveChangesAsync();
        return removed;
    }

    private static string NormalizeTitle(string title)
    {
        return title.Trim().ToLowerInvariant().Replace("  ", " ");
    }
}