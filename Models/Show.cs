namespace TvTimeViewer.Models;

public class Show
{
    public int Id { get; set; }
    public int? TvShowId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public byte[]? PosterImage { get; set; }
    public string? PosterUrl { get; set; }
    public string? PosterContentType { get; set; }
    public bool Followed { get; set; }
    public bool Archived { get; set; }
    public DateTime? LastWatchedAt { get; set; }
    public List<Episode> Episodes { get; set; } = new();
}