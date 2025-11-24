namespace YoutubeOcr.Core.Models;

public class VideoInfo
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }
    public string? ThumbnailPath { get; set; }
}
