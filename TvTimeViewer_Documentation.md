# TvTimeViewer — Project Documentation

## 1. Overview

TvTimeViewer is an ASP.NET Core MVC web application for tracking TV shows and movies you watch. It lets you search for titles, add them to a personal library, mark episodes as watched, browse trending and top-rated content, and view posters and metadata pulled live from external APIs.

**Tech stack:**
- ASP.NET Core MVC (.NET 10)
- Entity Framework Core (SQL database via migrations)
- External APIs: TVmaze, TMDb (The Movie Database), OMDb
- In-memory caching (`IMemoryCache`) for trending/top-rated data
- Razor views with vanilla JavaScript (fetch API) for live search and dynamic sections

---

## 2. Project Structure

```
TvTimeViewer/
├── Controllers/
│   ├── ShowController.cs         # Show details, episode tracking, watched toggles
│   ├── SearchController.cs       # OMDb search, live search, add-to-library
│   └── TrendingController.cs     # TMDb trending + top rated, add-to-library
├── Services/
│   ├── TvmazeService.cs          # Fetches episodes and posters from TVmaze
│   ├── OmdbService.cs            # Searches and fetches details from OMDb
│   └── PosterEnrichmentService.cs # Backfills missing posters for shows/movies
├── Models/
│   ├── Show.cs                   # TV show entity
│   ├── Movie.cs                  # Movie entity
│   ├── Episode.cs                # Episode entity (belongs to a Show)
│   └── OmdbSearchItem.cs         # DTO for OMDb search results
├── Data/
│   └── AppDbContext.cs           # EF Core DbContext (Shows, Movies, Episodes)
├── Views/
│   ├── Show/Details.cshtml       # Show detail page with episode list
│   ├── Search/Index.cshtml       # Live search + Top Rated sections
│   └── Library/Index.cshtml      # Your saved shows/movies + Trending section
└── Migrations/                   # EF Core migration history
```

---

## 3. Data Models

### Show
| Property | Type | Purpose |
|---|---|---|
| Id | int | Primary key |
| TvShowId | int? | TMDb ID (for shows added via Trending/Top Rated) |
| Title | string | Show title |
| Genre | string? | Comma-separated genre list |
| PosterImage | byte[]? | Downloaded poster binary |
| PosterUrl | string? | Original poster URL |
| PosterContentType | string? | MIME type of poster image |
| Followed | bool | Whether user follows this show |
| Archived | bool | Whether show is archived |
| LastWatchedAt | DateTime? | Timestamp of most recent watch |
| Episodes | List<Episode> | Navigation property |

### Episode
| Property | Type | Purpose |
|---|---|---|
| Id | int | Primary key |
| ShowId | int | Foreign key to Show |
| SeasonNumber | int | Season number |
| EpisodeNumber | int | Episode number within season |
| Name | string? | Episode title (from TVmaze) |
| AirDate | DateTime? | Original air date |
| Watched | bool | Watched flag |
| WatchedAt | DateTime? | Timestamp when marked watched |

### Movie
| Property | Type | Purpose |
|---|---|---|
| Id | int | Primary key |
| Title | string | Movie title |
| Genre | string? | Comma-separated genre list |
| PosterImage | byte[]? | Downloaded poster binary |
| PosterUrl | string? | Original poster URL |
| PosterContentType | string? | MIME type of poster image |
| Watched | bool | Watched flag |
| WatchedAt | DateTime? | Timestamp when marked watched |

---

## 4. Services

### TvmazeService
- `FetchEpisodesAsync(string title)` → returns a `List<Episode>` for a show by title, parsed from TVmaze's `/singlesearch/shows?embed=episodes` endpoint. `ShowId` must be assigned by the caller afterward.
- `FetchPosterAsync(string title)` → returns a poster URL string (or null) from TVmaze's show image data.

### OmdbService
- Handles searching OMDb by title and fetching full details by IMDb ID. Used by `SearchController` for the manual search bar.

### PosterEnrichmentService
- `EnrichAllAsync(Action<int,int>? onProgress)` → batch-fills missing posters for all shows/movies in the database using TVmaze (shows) and OMDb (movies), downloading and storing poster bytes directly.

---

## 5. Controllers & Key Actions

### ShowController
- `Details(int id)` — loads a show and its episodes; if no episodes exist yet, fetches them live from TVmaze and saves them.
- `ToggleWatched(int episodeId)` — flips an episode's watched status and updates the show's `LastWatchedAt`.
- `MarkSeasonWatched(int showId, int seasonNumber)` — marks all episodes in a season as watched.
- `MarkAllWatched(int showId)` — marks every episode of a show as watched.

### SearchController
- `Index(string? q)` — full-page OMDb search (fallback / initial load).
- `SearchJson(string? q)` — lightweight JSON endpoint used for live, debounced search-as-you-type.
- `Details(string id)` — OMDb detail view by IMDb ID.
- `AddToLibrary(string imdbId, string title, string poster, string type)` — adds an OMDb search result to your Shows or Movies library, downloading the poster.

### TrendingController
- `Top()` — returns this week's top 10 trending shows and movies from TMDb (cached 15 minutes).
- `TopRated()` — returns TMDb's all-time top rated shows and movies, live from the API, not the local database (cached 6 hours).
- `AddShow(int tmdbId)` / `AddMovie(int tmdbId)` — fetches full detail from TMDb by ID and inserts into the local database if not already present.

---

## 6. Key Features & How They Work

### Live Search (search-as-you-type)
The Search page's input box listens for `input` events, debounces 350ms, cancels any in-flight request via `AbortController`, and calls `/Search/SearchJson?q=...`. Results render dynamically without a page reload. If nothing matches, a "We're sorry, we couldn't find any matches" message displays, mirroring the reference TV Time app UI.

### Trending & Top Rated Sections
Both the Library page (Trending) and Search page (Top Rated) pull live data directly from TMDb via `fetch()` calls to `/Trending/Top` and `/Trending/TopRated`. These are **never read from the local database** — they always reflect TMDb's current lists. Each card has an "+ Add to List" button that posts to `/Trending/AddShow` or `/Trending/AddMovie`, which checks for duplicates before inserting.

### Episode Tracking
When you open a show's Details page for the first time, episodes are fetched live from TVmaze and cached into your database. From then on, watched status is tracked locally — toggling episodes, marking whole seasons, or marking an entire show as watched, updating `LastWatchedAt` on the show each time.

### Poster Enrichment
`PosterEnrichmentService.EnrichAllAsync` can be run (e.g., from a background job or admin action) to backfill missing posters for older library entries that don't yet have a `PosterImage`.

---

## 7. Setup & Installation Guide

### Prerequisites
- .NET 10 SDK installed
- SQL Server (or SQLite/LocalDB depending on your `AppDbContext` configuration)
- API keys for TMDb (`Tmdb:ApiKey`) and OMDb, set in `appsettings.json` or user secrets

### Configuration
Add to `appsettings.json`:
```json
{
  "Tmdb": {
    "ApiKey": "YOUR_TMDB_API_KEY"
  },
  "Omdb": {
    "ApiKey": "YOUR_OMDB_API_KEY"
  },
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_CONNECTION_STRING"
  }
}
```

Ensure `Program.cs` registers:
```csharp
builder.Services.AddHttpClient("tmdb");
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<TvmazeService>();
builder.Services.AddScoped<OmdbService>();
builder.Services.AddScoped<PosterEnrichmentService>();
```

### Running Migrations
```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Whenever you change a model (e.g., adding `AirDate` to `Episode`), repeat:
```powershell
dotnet ef migrations add <DescriptiveName>
dotnet ef database update
```

### Running the App
```powershell
dotnet build
dotnet run
```
Navigate to `https://localhost:5001` (or the port shown in the console).

---

## 8. Usage Guide

1. **Search for a title** — Go to the Search page and start typing; results appear automatically after a brief pause, no need to press Enter.
2. **Browse Top Rated** — Scroll down on the Search page to see TMDb's all-time top rated shows and movies, refreshed live from the API.
3. **Browse Trending** — Visit the Library page to see this week's trending shows and movies.
4. **Add to your library** — Click "+ Add to List" on any Trending/Top Rated card, or open an OMDb search result's Details page and click Add to Library.
5. **Track episodes** — Open any show in your library to see its full episode list (auto-fetched from TVmaze on first visit). Toggle individual episodes, mark a whole season watched, or mark the entire show watched.
6. **Review your library** — The Library page lists everything you've added, with posters, watched progress, and last-watched timestamps.

---

## 9. Common Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| `dotnet ef migrations add` fails with "Build failed" | Underlying C# compile errors | Run `dotnet build` first to see the real error |
| `CS0117: 'Episode' does not contain a definition for 'X'` | Model missing a property another file expects | Add the missing property to `Episode.cs`/`Show.cs`/`Movie.cs` |
| `CS1503` tuple/type mismatch in `PosterEnrichmentService.cs` | `TvmazeService` method signature changed | Ensure `FetchPosterAsync` returns `string?`, not a tuple |
| Top Rated / Trending shows "No results returned" | Missing or invalid TMDb API key | Check `Tmdb:ApiKey` in `appsettings.json` |
| Live search does nothing | JS not loaded or `SearchJson` action missing | Confirm `@section Scripts` renders and `SearchController.SearchJson` exists |

---

## 10. Architecture Notes

- **Trending and Top Rated data is always live** — it is fetched directly from TMDb on every page load (subject to short-term server-side caching) and is never stored in or read from the local database until a user explicitly clicks "Add to List."
- **Episodes are lazily fetched** — a show's episodes are only pulled from TVmaze the first time its Details page is visited; afterward they persist locally so watched status can be tracked.
- **Posters are stored as binary blobs** (`byte[]`) in the database rather than re-fetching from external URLs on every page load, improving reliability if the source URL later breaks.
