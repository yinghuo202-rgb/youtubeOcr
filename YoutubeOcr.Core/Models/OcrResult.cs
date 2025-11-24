namespace YoutubeOcr.Core.Models;

public class OcrResult
{
    public string VideoId { get; set; } = string.Empty;
    public int FrameIndex { get; set; }
    public TimeSpan Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBox? BoundingBox { get; set; }
}
