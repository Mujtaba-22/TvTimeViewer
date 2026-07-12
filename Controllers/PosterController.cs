using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;

namespace TvTimeViewer.Controllers;

public class PosterController : Controller
{
    private readonly AppDbContext _db;
    public PosterController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Show(int id)
    {
        var show = await _db.Shows.FindAsync(id);
        if (show?.PosterImage == null) return NotFound();
        return File(show.PosterImage, show.PosterContentType ?? "image/jpeg");
    }

    public async Task<IActionResult> Movie(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie?.PosterImage == null) return NotFound();
        return File(movie.PosterImage, movie.PosterContentType ?? "image/jpeg");
    }
}