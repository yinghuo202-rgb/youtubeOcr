namespace YoutubeOcr.Core.Models;

public class OcrRecord
{
    public string VideoId { get; set; } = string.Empty;
    public int FrameIndex { get; set; }
    public TimeSpan Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBox? BoundingBox { get; set; }
}

public class CleanResult
{
    public string VideoId { get; set; } = string.Empty;
    public int FrameIndex { get; set; }
    public TimeSpan Timestamp { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string CleanText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBox? BoundingBox { get; set; }
    public bool Dropped { get; set; }
}
