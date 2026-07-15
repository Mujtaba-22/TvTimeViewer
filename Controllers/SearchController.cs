using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Models;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class SearchController : Controller
{
    private readonly OmdbService _omdb;
    private readonly TmdbService _tmdb;
    private readonly AppDbContext _db;
    private readonly HttpClient _http;

    public SearchController(
        OmdbService omdb,
        TmdbService tmdb,
        AppDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _omdb = omdb;
        _tmdb = tmdb;
        _db = db;
        _http = httpClientFactory.CreateClient();
    }

    public async Task<IActionResult> Index(string? q)
    {
        var results = await SearchCombinedAsync(q);
        ViewBag.Query = q;
        return View(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string q, string type = "tv")
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new List<object>());

        try
        {
            var tmdbType = type == "movie" ? "movie" : "tv";
            var results = await _tmdb.SearchAsync(q, tmdbType);
            return Json(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<List<OmdbSearchItem>> SearchCombinedAsync(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new List<OmdbSearchItem>();

        var omdbResults = await _omdb.SearchAsync(q);
        var tmdbResults = await _tmdb.SearchAsync(q, "all");

        var existingTitles = new HashSet<string>(
            omdbResults
                .Where(r => !string.IsNullOrWhiteSpace(r.Title))
                .Select(r => r.Title.ToLowerInvariant())
        );

        var merged = new List<OmdbSearchItem>(omdbResults);

        merged.AddRange(
            tmdbResults.Where(t =>
                !string.IsNullOrWhiteSpace(t.Title) &&
                !existingTitles.Contains(t.Title.ToLowerInvariant()))
        );

        return merged;
    }

    public async Task<IActionResult> Details(string id)
    {
        if (id.StartsWith("tmdb-") && int.TryParse(id.Replace("tmdb-", ""), out var tmdbId))
        {
            var tvDetail = await _tmdb.GetDetailsAsync(tmdbId, "series");
            if (tvDetail != null && !string.IsNullOrEmpty(tvDetail.Title) && tvDetail.Title != "Untitled")
                return View(tvDetail);

            var movieDetail = await _tmdb.GetDetailsAsync(tmdbId, "movie");
            if (movieDetail != null)
                return View(movieDetail);

            return NotFound();
        }

        var detail = await _omdb.GetDetailsAsync(id);
        if (detail == null)
            return NotFound();

        return View(detail);
    }

    [HttpPost]
    public async Task<IActionResult> AddToLibrary(string imdbId, string title, string poster, string type)
    {
        var (imageBytes, contentType) = await DownloadPosterAsync(poster);

        if (type.Equals("series", StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.Shows.AnyAsync(s => s.ImdbId == imdbId);
            if (!exists)
            {
                _db.Shows.Add(new Show
                {
                    Title = title,
                    ImdbId = imdbId,
                    PosterUrl = poster,
                    PosterImage = imageBytes,
                    PosterContentType = contentType
                });

                await _db.SaveChangesAsync();
                TempData["Message"] = $"{title} added to your Shows library.";
            }
            else
            {
                TempData["Message"] = $"{title} is already in your library.";
            }
        }
        else
        {
            var exists = await _db.Movies.AnyAsync(m => m.ImdbId == imdbId);
            if (!exists)
            {
                _db.Movies.Add(new Movie
                {
                    Title = title,
                    ImdbId = imdbId,
                    PosterUrl = poster,
                    PosterImage = imageBytes,
                    PosterContentType = contentType
                });

                await _db.SaveChangesAsync();
                TempData["Message"] = $"{title} added to your Movies library.";
            }
            else
            {
                TempData["Message"] = $"{title} is already in your library.";
            }
        }

        return RedirectToAction("Index", "Library");
    }

    private async Task<(byte[]? bytes, string? contentType)> DownloadPosterAsync(string? posterUrl)
    {
        if (string.IsNullOrEmpty(posterUrl) || posterUrl == "N/A")
            return (null, null);

        try
        {
            var response = await _http.GetAsync(posterUrl);
            if (!response.IsSuccessStatusCode)
                return (null, null);

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