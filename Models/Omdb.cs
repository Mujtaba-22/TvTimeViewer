using System.Text.Json.Serialization;

namespace TvTimeViewer.Models;

public class OmdbSearchResponse
{
    [JsonPropertyName("Search")]
    public List<OmdbSearchItem> Search { get; set; } = new();
}

public class OmdbSearchItem
{
    public string Title { get; set; } = "";
    public string Year { get; set; } = "";
    public string imdbID { get; set; } = "";
    public string Type { get; set; } = "";
    public string Poster { get; set; } = "";
}

public class OmdbDetail
{
    public string Title { get; set; } = "";
    public string Year { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Plot { get; set; } = "";
    public string Poster { get; set; } = "";
    public string imdbRating { get; set; } = "";
    public string imdbVotes { get; set; } = "";
    public string imdbID { get; set; } = "";
    public string Type { get; set; } = "";
    public string Actors { get; set; } = "";
    public string Director { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string totalSeasons { get; set; } = "";
}
public class OmdbSeasonResponse
{
    public string? Title { get; set; }
    public string? Season { get; set; }
    public List<OmdbEpisodeItem>? Episodes { get; set; }
    public string? Response { get; set; }
}

public class OmdbEpisodeItem
{
    public string? Title { get; set; }
    public string? Released { get; set; }
    public string? Episode { get; set; }
}