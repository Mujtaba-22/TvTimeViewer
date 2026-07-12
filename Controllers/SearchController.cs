using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class SearchController : Controller
{
    private readonly OmdbService _omdb;
    private readonly AppDbContext _db;
    private readonly HttpClient _http;

    public SearchController(OmdbService omdb, AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _omdb = omdb; _db = db; _http = httpClientFactory.CreateClient();
    }

    public async Task<IActionResult> Index(string? q)
    {
        var results = string.IsNullOrWhiteSpace(q) ? new List<OmdbSearchItem>() : await _omdb.SearchAsync(q);
        ViewBag.Query = q;
        return View(results);
    }

    public async Task<IActionResult> Details(string id)
    {
        var detail = await _omdb.GetDetailsAsync(id);
        if (detail == null) return NotFound();
        return View(detail);
    }

    [HttpPost]
    public async Task<IActionResult> AddToLibrary(string imdbId, string title, string poster, string type)
    {
        var (imageBytes, contentType) = await DownloadPosterAsync(poster);

        if (type.Equals("series", StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.Shows.AnyAsync(s => s.Title == title);
            if (!exists)
            {
                _db.Shows.Add(new Show { Title = title, PosterUrl = poster, PosterImage = imageBytes, PosterContentType = contentType });
                await _db.SaveChangesAsync();
                TempData["Message"] = $"{title} added to your Shows library.";
            }
            else TempData["Message"] = $"{title} is already in your library.";
        }
        else
        {
            var exists = await _db.Movies.AnyAsync(m => m.Title == title);
            if (!exists)
            {
                _db.Movies.Add(new Movie { Title = title, PosterUrl = poster, PosterImage = imageBytes, PosterContentType = contentType });
                await _db.SaveChangesAsync();
                TempData["Message"] = $"{title} added to your Movies library.";
            }
            else TempData["Message"] = $"{title} is already in your library.";
        }

        return RedirectToAction("Index", "Library");
    }

    private async Task<(byte[]? bytes, string? contentType)> DownloadPosterAsync(string? posterUrl)
    {
        if (string.IsNullOrEmpty(posterUrl) || posterUrl == "N/A") return (null, null);
        try
        {
            var response = await _http.GetAsync(posterUrl);
            if (!response.IsSuccessStatusCode) return (null, null);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (bytes, contentType);
        }
        catch { return (null, null); }
    }
}