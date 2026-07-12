using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;

namespace TvTimeViewer.Controllers;

public class MovieController : Controller
{
    private readonly AppDbContext _db;
    public MovieController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Details(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie == null) return NotFound();
        return View(movie);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleWatched(int movieId)
    {
        var movie = await _db.Movies.FindAsync(movieId);
        if (movie != null)
        {
            movie.Watched = !movie.Watched;
            movie.WatchedAt = movie.Watched ? DateTime.UtcNow : null;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", "Library");
    }
}