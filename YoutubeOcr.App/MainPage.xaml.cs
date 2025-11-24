using System.Collections.ObjectModel;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;
using YoutubeOcr.Core.Services;

namespace YoutubeOcr.App;

public partial class MainPage : ContentPage
{
    private readonly IYouTubeDownloader _downloader;
    private readonly IFrameExtractor _extractor;
    private readonly IOcrEngine _ocr;

    private string? _lastVideoPath;
    private string? _lastFrameDir;

    public MainPage(
        IYouTubeDownloader downloader,
        IFrameExtractor extractor,
        IOcrEngine ocr)
    {
        InitializeComponent();
        _downloader = downloader;
        _extractor = extractor;
        _ocr = ocr;
    }

    private void AppendLog(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogEditor.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        });
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(UrlEntry.Text)
                ? "https://www.youtube.com/watch?v=ba7rRfKIHxU" // demo
                : UrlEntry.Text!.Trim();

            var config = new PipelineConfig();
            Directory.CreateDirectory(config.Download.OutputDirectory);

            var progress = new Progress<DownloadProgress>(p =>
            {
                AppendLog($"{p.Status}: {p.Url} {p.Message}");
            });

            var result = await _downloader.DownloadAsync(new[] { url }, config.Download, progress);
            var video = result.FirstOrDefault();
            if (video != null)
            {
                _lastVideoPath = video.LocalPath;
                VideoPathEntry.Text = video.LocalPath;
                AppendLog($"下载完成: {video.LocalPath}");
            }
            else
            {
                AppendLog("未获得下载文件");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"下载异常: {ex.Message}");
        }
    }

    private async void OnExtractClicked(object sender, EventArgs e)
    {
        try
        {
            var videoPath = string.IsNullOrWhiteSpace(VideoPathEntry.Text) ? _lastVideoPath : VideoPathEntry.Text;
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                AppendLog("请先填写有效的视频路径或先执行下载。");
                return;
            }

            var config = new PipelineConfig();
            Directory.CreateDirectory(config.FrameExtract.OutputDirectory);

            var progress = new Progress<FrameExtractProgress>(p =>
            {
                AppendLog($"{p.Status}: {p.VideoPath} {p.Message}");
            });

            var result = await _extractor.ExtractFramesAsync(videoPath, config.FrameExtract, progress);
            _lastFrameDir = result.OutputDirectory;
            FrameDirEntry.Text = result.OutputDirectory;
            AppendLog($"抽帧完成，输出目录: {result.OutputDirectory}，帧数: {result.Frames.Count}");
        }
        catch (Exception ex)
        {
            AppendLog($"抽帧异常: {ex.Message}");
        }
    }

    private async void OnOcrClicked(object sender, EventArgs e)
    {
        try
        {
            var frameDir = string.IsNullOrWhiteSpace(FrameDirEntry.Text) ? _lastFrameDir : FrameDirEntry.Text;
            if (string.IsNullOrWhiteSpace(frameDir) || !Directory.Exists(frameDir))
            {
                AppendLog("请先填写有效的帧目录或先执行抽帧。");
                return;
            }

            var frames = Directory.GetFiles(frameDir)
                .OrderBy(p => p)
                .Take(3)
                .Select((p, idx) => new FrameInfo
                {
                    VideoId = new DirectoryInfo(frameDir).Name,
                    FrameIndex = idx,
                    Timestamp = TimeSpan.FromSeconds(idx),
                    ImagePath = p
                })
                .ToList();

            if (frames.Count == 0)
            {
                AppendLog("帧目录为空。");
                return;
            }

            var config = new PipelineConfig();
            var progress = new Progress<string>(msg => AppendLog(msg));
            var results = await _ocr.RunAsync(frames, config.Ocr, progress);

            foreach (var r in results.Take(5))
            {
                AppendLog($"OCR: [{r.FrameIndex}] {r.Text} (Conf {r.Confidence:F2})");
            }

            if (results.Count == 0)
            {
                AppendLog("未识别到文本。");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"OCR 异常: {ex.Message}");
        }
    }
}
