using System.Globalization;
using System.Text;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;

namespace YoutubeOcr.Core.Services;

public record FrameExtractProgress(string VideoPath, string Status, int FramesExtracted, string? Message);
public record PreviewFrameResult(string? FilePath, string? Error);

public interface IFrameExtractor
{
    Task<FrameExtractResult> ExtractFramesAsync(
        string videoPath,
        FrameExtractConfig config,
        IProgress<FrameExtractProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<PreviewFrameResult> ExtractPreviewFrameAsync(
        string videoPath,
        FrameExtractConfig config,
        CancellationToken cancellationToken = default);
}

public class FrameExtractResult
{
    public string OutputDirectory { get; set; } = string.Empty;
    public IReadOnlyList<FrameInfo> Frames { get; set; } = Array.Empty<FrameInfo>();
}

public class FfmpegFrameExtractor : IFrameExtractor
{
    private readonly ToolLocator _toolLocator;
    private readonly IProcessRunner _processRunner;

    public FfmpegFrameExtractor(ToolLocator toolLocator, IProcessRunner processRunner)
    {
        _toolLocator = toolLocator;
        _processRunner = processRunner;
    }

    public async Task<FrameExtractResult> ExtractFramesAsync(
        string videoPath,
        FrameExtractConfig config,
        IProgress<FrameExtractProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found", videoPath);
        }

        var ffmpegPath = _toolLocator.ResolveFfmpeg(config.FfmpegPath);
        var videoId = Path.GetFileNameWithoutExtension(videoPath);
        var outputRoot = Path.Combine(config.OutputDirectory, videoId);
        Directory.CreateDirectory(outputRoot);

        var outputFormat = config.OutputFormat?.ToLowerInvariant() == "png" ? "png" : "jpg";
        var outputPattern = Path.Combine(outputRoot, $"frame_%06d.{outputFormat}");

        var args = BuildArguments(videoPath, outputPattern, config);

        progress?.Report(new FrameExtractProgress(videoPath, "working", 0, null));
        var result = await _processRunner.RunAsync(ffmpegPath, args, outputProgress: progress != null
                ? new Progress<string>(_ => { /* ignore line output, we only care about completion */ })
                : null, cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            progress?.Report(new FrameExtractProgress(videoPath, "failed", 0, result.StandardError));
            return new FrameExtractResult { OutputDirectory = outputRoot };
        }

        var frames = Directory.GetFiles(outputRoot)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select((path, idx) => new FrameInfo
            {
                VideoId = videoId,
                FrameIndex = idx,
                Timestamp = TimeSpan.FromSeconds(idx / Math.Max(config.TargetFps, 0.01)),
                ImagePath = path,
                CropBox = config.CropRect is null
                    ? null
                    : new BoundingBox(config.CropRect.X, config.CropRect.Y, config.CropRect.Width, config.CropRect.Height)
            })
            .ToList();

        progress?.Report(new FrameExtractProgress(videoPath, "done", frames.Count, null));
        return new FrameExtractResult
        {
            OutputDirectory = outputRoot,
            Frames = frames
        };
    }

    private static string BuildArguments(string videoPath, string outputPattern, FrameExtractConfig config)
    {
        var filters = new List<string>();
        if (config.TargetFps > 0)
        {
            filters.Add($"fps={config.TargetFps.ToString(CultureInfo.InvariantCulture)}");
        }

        if (config.CropRect != null)
        {
            var rect = config.CropRect;
            filters.Add($"crop={rect.Width}:{rect.Height}:{rect.X}:{rect.Y}");
        }

        if (config.ResizeWidth.HasValue || config.ResizeHeight.HasValue)
        {
            var widthText = config.ResizeWidth?.ToString(CultureInfo.InvariantCulture) ?? "-1";
            var heightText = config.ResizeHeight?.ToString(CultureInfo.InvariantCulture) ?? "-1";
            filters.Add($"scale={widthText}:{heightText}");
        }

        var sb = new StringBuilder();
        if (config.Start.HasValue)
        {
            sb.Append($"-ss {FormatTime(config.Start.Value)} ");
        }

        sb.Append($"-i \"{videoPath}\" ");

        if (config.End.HasValue)
        {
            sb.Append($"-to {FormatTime(config.End.Value)} ");
        }

        if (filters.Count > 0)
        {
            sb.Append($"-vf \"{string.Join(',', filters)}\" ");
        }

        sb.Append($"-q:v 2 -y \"{outputPattern}\"");
        return sb.ToString();
    }

    private static string FormatTime(TimeSpan ts)
    {
        return ts.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    public async Task<PreviewFrameResult> ExtractPreviewFrameAsync(string videoPath, FrameExtractConfig config, CancellationToken cancellationToken = default)
    {
        var ffmpegPath = _toolLocator.ResolveFfmpeg(config.FfmpegPath);
        var previewFile = Path.Combine(Path.GetTempPath(), $"maui_preview_{Guid.NewGuid():N}.jpg");

        // Try configured position first, then a start-of-video fallback to avoid hard failures.
        var positions = new List<TimeSpan>
        {
            config.Start ?? TimeSpan.FromSeconds(1),
            TimeSpan.Zero
        };

        string? lastError = null;
        foreach (var pos in positions)
        {
            try
            {
                var args = $"-ss {FormatTime(pos)} -i \"{videoPath}\" -frames:v 1 -y \"{previewFile}\"";
                var result = await _processRunner.RunAsync(ffmpegPath, args, cancellationToken: cancellationToken);

                if (result.IsSuccess && File.Exists(previewFile))
                {
                    return new PreviewFrameResult(previewFile, null);
                }

                lastError = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new PreviewFrameResult(null, lastError ?? "ffmpeg 预览失败");
    }
}
