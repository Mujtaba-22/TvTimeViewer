using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class ShowController : Controller
{
    private readonly AppDbContext _db;
    private readonly TvmazeService _tvmaze;
    private readonly OmdbService _omdb;
    private readonly TmdbService _tmdb;

    public ShowController(AppDbContext db, TvmazeService tvmaze, OmdbService omdb, TmdbService tmdb)
    {
        _db = db;
        _tvmaze = tvmaze;
        _omdb = omdb;
        _tmdb = tmdb;
    }

    private async Task<List<Episode>> FetchEpisodesWithFallbackAsync(string title, string? imdbId = null)
    {
        if (!string.IsNullOrEmpty(imdbId) &&
            imdbId.StartsWith("tmdb-") &&
            int.TryParse(imdbId.Replace("tmdb-", ""), out var tmdbId))
        {
            var directEpisodes = await _tmdb.FetchEpisodesByIdAsync(tmdbId);
            if (directEpisodes.Any()) return directEpisodes;
        }

        var episodes = await _tvmaze.FetchEpisodesAsync(title);

        if (!episodes.Any())
        {
            episodes = await _omdb.FetchEpisodesAsync(title);
        }

        if (!episodes.Any())
        {
            episodes = await _tmdb.FetchEpisodesAsync(title);
        }

        return episodes;
    }

    private async Task<List<Episode>> GetOrFetchEpisodesAsync(Show show)
    {
        if (show.Episodes.Any()) return show.Episodes;

        var fetched = await FetchEpisodesWithFallbackAsync(show.Title, show.ImdbId);

        foreach (var ep in fetched)
        {
            ep.ShowId = show.Id;
        }

        _db.Episodes.AddRange(fetched);
        await _db.SaveChangesAsync();

        show.Episodes = fetched;
        return show.Episodes;
    }

    public async Task<IActionResult> Details(int id)
    {
        var show = await _db.Shows
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (show == null) return NotFound();

        if (!show.Episodes.Any())
        {
            await GetOrFetchEpisodesAsync(show);
        }
        else if (show.Episodes.Any(e => IsTba(e.Name)))
        {
            await RefreshTbaEpisodeNamesAsync(show.Id, show.Title, show.ImdbId, show.Episodes);
        }

        return View(show);
    }

    private static bool IsAjaxRequest(HttpRequest request)
    {
        return request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }

    private static bool IsTba(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            || name.Trim().Equals("TBA", StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("TBD", StringComparison.OrdinalIgnoreCase)
            || name.Trim().Equals("Untitled", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshTbaEpisodeNamesAsync(int showId, string showTitle, string? imdbId, List<Episode> existingEpisodes)
    {
        try
        {
            var freshEpisodes = await FetchEpisodesWithFallbackAsync(showTitle, imdbId);
            if (freshEpisodes == null || !freshEpisodes.Any()) return;

            var freshLookup = freshEpisodes
                .GroupBy(e => (e.SeasonNumber, e.EpisodeNumber))
                .ToDictionary(g => g.Key, g => g.First());

            var anyUpdated = false;

            foreach (var existing in existingEpisodes.Where(e => IsTba(e.Name)))
            {
                if (freshLookup.TryGetValue((existing.SeasonNumber, existing.EpisodeNumber), out var freshMatch)
                    && !IsTba(freshMatch.Name))
                {
                    existing.Name = freshMatch.Name;

                    if (existing.AirDate == null && freshMatch.AirDate != null)
                    {
                        existing.AirDate = freshMatch.AirDate;
                    }

                    anyUpdated = true;
                }
            }

            if (anyUpdated)
            {
                await _db.SaveChangesAsync();
            }
        }
        catch
        {
            // Keep existing data if external providers fail.
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleWatched(int episodeId)
    {
        var episode = await _db.Episodes.FindAsync(episodeId);

        if (episode == null)
        {
            if (IsAjaxRequest(Request))
            {
                return Json(new
                {
                    success = false,
                    message = "Episode not found."
                });
            }

            TempData["Error"] = "Episode not found.";
            return RedirectToAction("Index", "Library");
        }

        var isReleased = episode.AirDate.HasValue && episode.AirDate.Value.Date <= DateTime.UtcNow.Date;

        if (!isReleased)
        {
            if (IsAjaxRequest(Request))
            {
                return Json(new
                {
                    success = false,
                    message = "This episode hasn't been released yet and can't be marked as watched."
                });
            }

            TempData["Error"] = "This episode hasn't been released yet and can't be marked as watched.";
            return RedirectToAction("Details", new { id = episode.ShowId });
        }

        episode.Watched = !episode.Watched;
        episode.WatchedAt = episode.Watched ? DateTime.UtcNow : null;

        var show = await _db.Shows
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == episode.ShowId);

        if (show != null)
        {
            if (episode.Watched)
            {
                show.LastWatchedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            if (IsAjaxRequest(Request))
            {
                var seasonEpisodes = show.Episodes
                    .Where(e => e.SeasonNumber == episode.SeasonNumber)
                    .ToList();

                var watchedInSeason = seasonEpisodes.Count(e => e.Watched);
                var totalInSeason = seasonEpisodes.Count;

                var watchedInShow = show.Episodes.Count(e => e.Watched);
                var totalInShow = show.Episodes.Count;

                return Json(new
                {
                    success = true,
                    watched = episode.Watched,
                    episodeId = episode.Id,
                    seasonNumber = episode.SeasonNumber,
                    watchedInSeason,
                    totalInSeason,
                    watchedInShow,
                    totalInShow
                });
            }
        }
        else
        {
            await _db.SaveChangesAsync();

            if (IsAjaxRequest(Request))
            {
                return Json(new
                {
                    success = true,
                    watched = episode.Watched,
                    episodeId = episode.Id
                });
            }
        }

        return RedirectToAction("Details", new { id = episode.ShowId });
    }

    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MarkSeasonWatched(int showId, int seasonNumber)
    {
        var show = await _db.Shows
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == showId);

        if (show == null) return NotFound();

        await GetOrFetchEpisodesAsync(show);

        var today = DateTime.UtcNow.Date;

        var episodesToMark = show.Episodes
            .Where(e => e.SeasonNumber == seasonNumber
                        && e.AirDate.HasValue
                        && e.AirDate.Value.Date <= today)
            .ToList();

        foreach (var ep in episodesToMark)
        {
            ep.Watched = true;
            ep.WatchedAt = DateTime.UtcNow;
        }

        if (episodesToMark.Any())
        {
            show.LastWatchedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return RedirectToAction("Details", new { id = showId });
    }

    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MarkAllWatched(int showId, string? returnUrl = null)
    {
        var show = await _db.Shows
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == showId);

        if (show == null) return NotFound();

        await GetOrFetchEpisodesAsync(show);

        var today = DateTime.UtcNow.Date;

        var episodesToMark = show.Episodes
            .Where(e => e.AirDate.HasValue && e.AirDate.Value.Date <= today)
            .ToList();

        foreach (var ep in episodesToMark)
        {
            ep.Watched = true;
            ep.WatchedAt = DateTime.UtcNow;
        }

        if (episodesToMark.Any())
        {
            show.LastWatchedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Details", new { id = showId });
    }
}