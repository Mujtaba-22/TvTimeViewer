namespace TvTimeViewer.Models;

public class Manga
{
    public int Id { get; set; }
    public string? MangaDexId { get; set; }
    public int? AniListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Format { get; set; } = "Manga";
    public string? Genre { get; set; }
    public byte[]? CoverImage { get; set; }
    public string? CoverUrl { get; set; }
    public string? CoverContentType { get; set; }
    public int ChaptersRead { get; set; } = 0;
    public int? TotalChapters { get; set; }
    public bool Following { get; set; }
    public bool Completed { get; set; }
    public DateTime? LastReadAt { get; set; }
    public List<MangaChapter> Chapters { get; set; } = new();
}