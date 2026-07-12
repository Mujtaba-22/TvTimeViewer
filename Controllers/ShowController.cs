using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class ShowController : Controller
{
    private readonly AppDbContext _db;
    private readonly TvmazeService _tvmaze;

    public ShowController(AppDbContext db, TvmazeService tvmaze)
    {
        _db = db;
        _tvmaze = tvmaze;
    }

    public async Task<IActionResult> Details(int id)
    {
        var show = await _db.Shows.Include(s => s.Episodes).FirstOrDefaultAsync(s => s.Id == id);
        if (show == null) return NotFound();

        if (!show.Episodes.Any())
        {
            var fetched = await _tvmaze.FetchEpisodesAsync(show.Title);
            foreach (var ep in fetched) ep.ShowId = show.Id;
            _db.Episodes.AddRange(fetched);
            await _db.SaveChangesAsync();
            show.Episodes = fetched;
        }

        return View(show);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleWatched(int episodeId)
    {
        var episode = await _db.Episodes.FindAsync(episodeId);
        if (episode != null)
        {
            episode.Watched = !episode.Watched;
            episode.WatchedAt = episode.Watched ? DateTime.UtcNow : null;

            var show = await _db.Shows.FindAsync(episode.ShowId);
            if (show != null && episode.Watched) show.LastWatchedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Details", new { id = episode?.ShowId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkSeasonWatched(int showId, int seasonNumber)
    {
        var episodes = await _db.Episodes
            .Where(e => e.ShowId == showId && e.SeasonNumber == seasonNumber)
            .ToListAsync();

        foreach (var ep in episodes)
        {
            ep.Watched = true;
            ep.WatchedAt = DateTime.UtcNow;
        }

        var show = await _db.Shows.FindAsync(showId);
        if (show != null) show.LastWatchedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction("Details", new { id = showId });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllWatched(int showId)
    {
        var episodes = await _db.Episodes
            .Where(e => e.ShowId == showId)
            .ToListAsync();

        foreach (var ep in episodes)
        {
            ep.Watched = true;
            ep.WatchedAt = DateTime.UtcNow;
        }

        var show = await _db.Shows.FindAsync(showId);
        if (show != null) show.LastWatchedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToAction("Details", new { id = showId });
    }
}