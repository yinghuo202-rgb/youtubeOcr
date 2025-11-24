using System.Text.Json.Serialization;
using YoutubeOcr.Core.Models;

namespace YoutubeOcr.Core.Config;

public class PipelineConfig
{
    public DownloadConfig Download { get; set; } = new();
    public FrameExtractConfig FrameExtract { get; set; } = new();
    public OcrConfig Ocr { get; set; } = new();

    [JsonIgnore]
    public static string DefaultConfigFileName => "pipeline.config.json";
}

public class DownloadConfig
{
    public string OutputDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "videos");
    public int MaxConcurrency { get; set; } = 2;
    public bool CheckForUpdates { get; set; } = true;
    public bool VideoOnly { get; set; } = true;
    public string FileNameTemplate { get; set; } = "{videoId}.mp4";
    public string? YtDlpPath { get; set; }
    public string? CookiesFile { get; set; }
    public string? CookiesFromBrowser { get; set; } = "chrome";
    public string FormatSelector { get; set; } = "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]";
    public string ExtractorArgs { get; set; } = "youtube:player_client=default";
}

public class FrameExtractConfig
{
    public string OutputDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "frames");
    public double TargetFps { get; set; } = 1.0;
    public TimeSpan? Start { get; set; }
    public TimeSpan? End { get; set; }
    public CropRect? CropRect { get; set; }
    public string OutputFormat { get; set; } = "jpg";
    public int? ResizeWidth { get; set; }
    public int? ResizeHeight { get; set; }
    public string? PreviewPosition { get; set; } = "mid";
    public string? FfmpegPath { get; set; }
}

public class OcrConfig
{
    public string Engine { get; set; } = "windows";
    public string Language { get; set; } = "zh-CN";
    public double ConfidenceThreshold { get; set; } = 0.5;
    public bool EnableDeduplication { get; set; } = true;
    public double DeduplicationWindowSeconds { get; set; } = 1.0;
    public string OutputDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "outputs");
    public string OutputFormat { get; set; } = "csv";
}

public record CropRect(int X, int Y, int Width, int Height);
