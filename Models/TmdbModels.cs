using System.Text.Json.Serialization;

namespace TvTimeViewer.Models;

public class TmdbSearchResponse<T>
{
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = new();
}

public class TmdbTvSearchResult
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Original_Name { get; set; }
    public string? First_Air_Date { get; set; }
    public string? Poster_Path { get; set; }
}

public class TmdbMovieSearchResult
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Original_Title { get; set; }
    public string? Release_Date { get; set; }
    public string? Poster_Path { get; set; }
}

public class TmdbTvDetails
{
    public int Id { get; set; }
    public int Number_Of_Seasons { get; set; }
}

public class TmdbSeasonResponse
{
    public List<TmdbEpisodeItem>? Episodes { get; set; }
}

public class TmdbEpisodeItem
{
    public string? Name { get; set; }
    public string? Air_Date { get; set; }
    public int Episode_Number { get; set; }
    public int Season_Number { get; set; }
}