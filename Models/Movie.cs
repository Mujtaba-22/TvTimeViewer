namespace TvTimeViewer.Models;

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public byte[]? PosterImage { get; set; }
    public string? PosterUrl { get; set; }
    public string? PosterContentType { get; set; }
    public bool Watched { get; set; }
    public DateTime? WatchedAt { get; set; }
}