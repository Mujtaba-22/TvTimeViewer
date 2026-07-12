using Microsoft.AspNetCore.Mvc;
using TvTimeViewer.Services;

namespace TvTimeViewer.Controllers;

public class ProgressController : Controller
{
    [HttpGet]
    public IActionResult Get(string key)
    {
        var (percent, message) = ProgressTracker.Get(key);
        return Json(new { percent, message });
    }
}