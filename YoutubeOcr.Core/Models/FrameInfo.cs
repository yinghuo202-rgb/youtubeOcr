namespace YoutubeOcr.Core.Models;

public class FrameInfo
{
    public string VideoId { get; set; } = string.Empty;
    public int FrameIndex { get; set; }
    public TimeSpan Timestamp { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public BoundingBox? CropBox { get; set; }
}
