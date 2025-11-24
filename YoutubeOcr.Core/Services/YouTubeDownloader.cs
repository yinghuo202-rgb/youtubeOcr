using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;

namespace YoutubeOcr.Core.Services;

public record DownloadProgress(string Url, string Status, double? Percent, string? Message, string? LocalPath);

public interface IYouTubeDownloader
{
    Task<IReadOnlyList<VideoInfo>> DownloadAsync(
        IEnumerable<string> urls,
        DownloadConfig config,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public class YouTubeDownloader : IYouTubeDownloader
{
    private readonly ToolLocator _toolLocator;
    private readonly IProcessRunner _processRunner;

    public YouTubeDownloader(ToolLocator toolLocator, IProcessRunner processRunner)
    {
        _toolLocator = toolLocator;
        _processRunner = processRunner;
    }

    public async Task<IReadOnlyList<VideoInfo>> DownloadAsync(
        IEnumerable<string> urls,
        DownloadConfig config,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var urlList = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        var results = new ConcurrentBag<VideoInfo>();
        Directory.CreateDirectory(config.OutputDirectory);

        var ytDlpPath = _toolLocator.ResolveYtDlp(config.YtDlpPath);

        if (config.CheckForUpdates)
        {
            _ = _processRunner.RunAsync(ytDlpPath, "-U", outputProgress: progress != null
                ? new Progress<string>(msg => progress.Report(new DownloadProgress(string.Empty, "更新", null, msg, null)))
                : null, cancellationToken: cancellationToken);
        }

        foreach (var url in urlList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var videoId = TryParseVideoId(url) ?? Guid.NewGuid().ToString("N")[..8];
            var targetTemplate = config.FileNameTemplate
                .Replace("{videoId}", videoId, StringComparison.OrdinalIgnoreCase)
                .Replace("{title}", "%(title)s", StringComparison.OrdinalIgnoreCase);

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append($"--newline -o \"{Path.Combine(config.OutputDirectory, targetTemplate)}\" --no-colors ");
            var format = string.IsNullOrWhiteSpace(config.FormatSelector) ? "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]" : config.FormatSelector;
            argumentsBuilder.Append($"-f \"{format}\" --merge-output-format mp4 ");

            if (!string.IsNullOrWhiteSpace(config.CookiesFile))
            {
                argumentsBuilder.Append($"--cookies \"{config.CookiesFile}\" ");
            }
            else if (!string.IsNullOrWhiteSpace(config.CookiesFromBrowser))
            {
                argumentsBuilder.Append($"--cookies-from-browser \"{config.CookiesFromBrowser}\" ");
            }

            if (!string.IsNullOrWhiteSpace(config.ExtractorArgs))
            {
                argumentsBuilder.Append($"--extractor-args \"{config.ExtractorArgs}\" ");
            }

            argumentsBuilder.Append($"\"{url}\"");
            var arguments = argumentsBuilder.ToString();

            progress?.Report(new DownloadProgress(url, "下载中", 0, null, null));
            var result = await _processRunner.RunAsync(ytDlpPath, arguments,
                outputProgress: progress != null
                    ? new Progress<string>(msg => progress.Report(new DownloadProgress(url, "下载中", null, msg, null)))
                    : null,
                cancellationToken: cancellationToken);

            if (!result.IsSuccess)
            {
                progress?.Report(new DownloadProgress(url, "失败", null, result.StandardError, null));
                continue;
            }

            var downloadedFile = FindLatestFile(config.OutputDirectory, videoId);
            var info = new VideoInfo
            {
                VideoId = videoId,
                LocalPath = downloadedFile ?? string.Empty,
                Title = downloadedFile is null ? videoId : Path.GetFileNameWithoutExtension(downloadedFile)
            };
            results.Add(info);
            progress?.Report(new DownloadProgress(url, "成功", 100, null, downloadedFile));
        }

        return results.ToList();
    }

    private static string? TryParseVideoId(string url)
    {
        var patterns = new[]
        {
            new Regex(@"v=([A-Za-z0-9_\-]{6,})", RegexOptions.IgnoreCase),
            new Regex(@"youtu\.be/([A-Za-z0-9_\-]{6,})", RegexOptions.IgnoreCase)
        };

        foreach (var regex in patterns)
        {
            var match = regex.Match(url);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? FindLatestFile(string directory, string videoId)
    {
        if (!Directory.Exists(directory)) return null;
        var candidates = Directory.GetFiles(directory)
            .Where(p => Path.GetFileName(p).Contains(videoId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToList();

        return candidates.FirstOrDefault();
    }
}
