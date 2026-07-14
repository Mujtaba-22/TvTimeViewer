namespace TvTimeViewer.Models;

public class Game
{
    public int Id { get; set; }
    public int IgdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public string? Platform { get; set; }
    public byte[]? CoverImage { get; set; }
    public string? CoverContentType { get; set; }
    public string? CoverUrl { get; set; }
    public double? Rating { get; set; }
    public int? ReleaseYear { get; set; }
    public bool Completed { get; set; }
    public bool Playing { get; set; }
    public double HoursPlayed { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}