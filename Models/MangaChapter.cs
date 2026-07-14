namespace TvTimeViewer.Models;

public class MangaChapter
{
    public int Id { get; set; }
    public int MangaId { get; set; }
    public int ChapterNumber { get; set; }
    public bool Read { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}