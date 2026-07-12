namespace TvTimeViewer.Models;

public class Episode
{
    public int Id { get; set; }
    public int ShowId { get; set; }
    public Show Show { get; set; } = null!;
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime? AirDate { get; set; }
    public bool Watched { get; set; }
    public DateTime? WatchedAt { get; set; }
}