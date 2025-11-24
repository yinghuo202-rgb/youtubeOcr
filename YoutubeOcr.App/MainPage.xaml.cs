using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using YoutubeOcr.App.Platform;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;
using YoutubeOcr.Core.Services;

namespace YoutubeOcr.App;

public partial class MainPage : ContentPage
{
    private enum StepSection
    {
        Download,
        Frames,
        Ocr,
        More
    }

    private class DownloadItem : INotifyPropertyChanged
    {
        private string _status = "待开始";
        public string Url { get; }
        public string? VideoId { get; set; }
        public string? LocalPath { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        public DownloadItem(string url)
        {
            Url = url;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private class VideoOption
    {
        public string Display { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public override string ToString() => Display;
    }

    private readonly IYouTubeDownloader _downloader;
    private readonly IFrameExtractor _extractor;
    private readonly IOcrEngine _ocr;

    private readonly ObservableCollection<DownloadItem> _downloadItems = new();
    private readonly ObservableCollection<VideoOption> _videoOptions = new();
    private readonly ObservableCollection<OcrResult> _ocrResults = new();
    private readonly ObservableCollection<string> _logMessages = new();
    private readonly FrameExtractConfig _frameConfig;

    private string? _lastVideoPath;
    private string? _lastFrameDir;
    private string _downloadOutputPath;
    private string _frameOutputPath;
    private IReadOnlyList<FrameInfo> _lastFramesForOcr = Array.Empty<FrameInfo>();
    private string? _previewFramePath;
    private StepSection _currentStep = StepSection.Download;

    public ObservableCollection<string> LogMessages => _logMessages;

    public MainPage(
        IYouTubeDownloader downloader,
        IFrameExtractor extractor,
        IOcrEngine ocr)
    {
        BindingContext = this;
        InitializeComponent();

        _downloader = downloader;
        _extractor = extractor;
        _ocr = ocr;

        DownloadListView.ItemsSource = _downloadItems;
        VideoPicker.ItemsSource = _videoOptions;
        OcrResultsView.ItemsSource = _ocrResults;

        var defaults = new PipelineConfig();
        _frameConfig = defaults.FrameExtract;
        _downloadOutputPath = defaults.Download.OutputDirectory;
        _frameOutputPath = defaults.FrameExtract.OutputDirectory;
        DownloadOutputEntry.Text = _downloadOutputPath;
        FrameOutputEntry.Text = _frameOutputPath;

        OcrLanguagePicker.SelectedIndex = 0;
        OcrConfidenceValueLabel.Text = OcrConfidenceSlider.Value.ToString("0.00");

        AppVersionLabel.Text = typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0";
        ConfigPathLabel.Text = Path.Combine(FileSystem.AppDataDirectory, PipelineConfig.DefaultConfigFileName);
        ToolsPathLabel.Text = Path.Combine(Directory.GetCurrentDirectory(), "Tools");
        LogPathLabel.Text = Path.Combine(FileSystem.AppDataDirectory, "Logs");

        SetStep(_currentStep);
        Log("界面初始化完成");
    }

    private void Log(string message, Exception? ex = null)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _logMessages.Add(line);
                if (_logMessages.Count > 500)
                {
                    _logMessages.RemoveAt(0);
                }

                FileLogger.LogInfo(line);
                if (ex != null)
                {
                    FileLogger.LogError(ex, message);
                }
            }
            catch
            {
                // keep logging silent
            }
        });
    }

    #region Navigation

    private void OnNavButtonClicked(object sender, EventArgs e)
    {
        try
        {
            var step = sender switch
            {
                Button b when b == NavDownloadButton => StepSection.Download,
                Button b when b == NavFramesButton => StepSection.Frames,
                Button b when b == NavOcrButton => StepSection.Ocr,
                Button b when b == NavMoreButton => StepSection.More,
                _ => _currentStep
            };

            SetStep(step);
        }
        catch (Exception ex)
        {
            Log($"导航切换异常: {ex.Message}", ex);
        }
    }

    private void SetStep(StepSection step)
    {
        _currentStep = step;
        DownloadContent.IsVisible = step == StepSection.Download;
        FrameContent.IsVisible = step == StepSection.Frames;
        OcrContent.IsVisible = step == StepSection.Ocr;
        MoreContent.IsVisible = step == StepSection.More;

        ApplyNavStyle(NavDownloadButton, step == StepSection.Download);
        ApplyNavStyle(NavFramesButton, step == StepSection.Frames);
        ApplyNavStyle(NavOcrButton, step == StepSection.Ocr);
        ApplyNavStyle(NavMoreButton, step == StepSection.More);

        StepTitleLabel.Text = step switch
        {
            StepSection.Download => "Step 1 · Download",
            StepSection.Frames => "Step 2 · Frames",
            StepSection.Ocr => "Step 3 · OCR",
            StepSection.More => "Step 4 · More / Settings",
            _ => StepTitleLabel.Text
        };

        StepSubtitleLabel.Text = step switch
        {
            StepSection.Download => "导入链接，像素猫陪你跑完整个流程",
            StepSection.Frames => "抽帧与 ROI 参数可视化，沿用原逻辑",
            StepSection.Ocr => "OCR 识别输出，界面更可爱",
            StepSection.More => "预留设置与结果展示",
            _ => StepSubtitleLabel.Text
        };
    }

    private void ApplyNavStyle(Button button, bool isSelected)
    {
        var key = isSelected ? "NavButtonSelectedStyle" : "NavButtonStyle";
        if (Resources.TryGetValue(key, out var styleObj) && styleObj is Style style)
        {
            button.Style = style;
        }
    }

    #endregion

    #region Download

    private DownloadItem? AddUrlToQueue(string? url)
    {
        var trimmed = url?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            Log("请输入有效链接");
            return null;
        }

        var existing = _downloadItems.FirstOrDefault(x => string.Equals(x.Url, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        var item = new DownloadItem(trimmed);
        _downloadItems.Add(item);
        return item;
    }

    private void OnAddUrlClicked(object sender, EventArgs e)
    {
        try
        {
            AddUrlToQueue(UrlEntry.Text);
        }
        catch (Exception ex)
        {
            Log($"添加链接失败: {ex.Message}", ex);
        }
    }

    private void OnClearUrlClicked(object sender, EventArgs e)
    {
        try
        {
            UrlEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Log($"清空输入失败: {ex.Message}", ex);
        }
    }

    private void OnImportUrlsClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(BatchUrlsEditor.Text)) return;
            var lines = BatchUrlsEditor.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                AddUrlToQueue(line);
            }
        }
        catch (Exception ex)
        {
            Log($"批量导入失败: {ex.Message}", ex);
        }
    }

    private void OnRemoveUrlClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is not Button btn || btn.CommandParameter is not DownloadItem item) return;
            _downloadItems.Remove(item);
        }
        catch (Exception ex)
        {
            Log($"移除链接失败: {ex.Message}", ex);
        }
    }

    private void OnClearQueueClicked(object sender, EventArgs e)
    {
        try
        {
            _downloadItems.Clear();
        }
        catch (Exception ex)
        {
            Log($"清空队列失败: {ex.Message}", ex);
        }
    }

    private void OnFillSampleClicked(object sender, EventArgs e)
    {
        try
        {
            UrlEntry.Text = "https://www.youtube.com/watch?v=ba7rRfKIHxU";
            AddUrlToQueue(UrlEntry.Text);
        }
        catch (Exception ex)
        {
            Log($"填充示例失败: {ex.Message}", ex);
        }
    }

    private async void OnBrowseDownloadFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var folder = await FolderPickerService.PickFolderAsync("选择下载目录");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _downloadOutputPath = folder;
                DownloadOutputEntry.Text = folder;
            }
        }
        catch (Exception ex)
        {
            Log($"选择下载目录失败: {ex.Message}", ex);
        }
    }

    private void OnClearSelectionClicked(object sender, EventArgs e)
    {
        try
        {
            UrlEntry.Text = string.Empty;
            BatchUrlsEditor.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Log($"清理输入失败: {ex.Message}", ex);
        }
    }

    private void SetDownloadProgress(double progress, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DownloadProgressBar.Progress = Math.Clamp(progress, 0, 1);
            DownloadProgressLabel.Text = message;
        });
    }
    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_downloadOutputPath))
            {
                Log("请先选择下载目录");
                return;
            }

            try
            {
                Directory.CreateDirectory(_downloadOutputPath);
            }
            catch (Exception ex)
            {
                Log($"无法创建下载目录：{_downloadOutputPath}", ex);
                return;
            }

            var targets = _downloadItems.Where(i => !string.Equals(i.Status, "成功", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!targets.Any() && !string.IsNullOrWhiteSpace(UrlEntry.Text))
            {
                var added = AddUrlToQueue(UrlEntry.Text);
                if (added != null)
                {
                    targets.Add(added);
                }
            }

            if (!targets.Any())
            {
                Log("请先添加需要下载的链接");
                return;
            }

            var config = new PipelineConfig();
            config.Download.OutputDirectory = _downloadOutputPath;
            config.Download.CheckForUpdates = AutoUpdateSwitch.IsToggled && AutoUpdateYtDlpSwitch.IsToggled;
            config.Download.VideoOnly = VideoOnlySwitch.IsToggled;

            SetDownloadProgress(0, "准备下载");

            var progress = new Progress<DownloadProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var item = _downloadItems.FirstOrDefault(x => string.Equals(x.Url, p.Url, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.Status = $"{p.Status} {(p.Percent?.ToString("0") ?? string.Empty)}%";
                        if (!string.IsNullOrWhiteSpace(p.LocalPath))
                        {
                            item.LocalPath = p.LocalPath;
                            item.VideoId = Path.GetFileNameWithoutExtension(p.LocalPath);
                            _lastVideoPath = p.LocalPath;
                        }
                    }

                    SetDownloadProgress(Math.Clamp(p.Percent ?? 0, 0, 100) / 100.0, $"{p.Status} {p.Message}");
                    if (!string.IsNullOrWhiteSpace(p.Url))
                    {
                        Log($"{p.Status}: {p.Url} {p.Message}");
                    }
                });
            });

            IReadOnlyList<VideoInfo> videos;
            try
            {
                videos = await _downloader.DownloadAsync(targets.Select(t => t.Url), config.Download, progress);
            }
            catch (FileNotFoundException ex)
            {
                Log($"未找到下载工具：{ex.FileName ?? ex.Message}", ex);
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var video in videos)
                {
                    var item = _downloadItems.FirstOrDefault(x =>
                        (!string.IsNullOrWhiteSpace(x.VideoId) && x.VideoId.Equals(video.VideoId, StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(Path.GetFileNameWithoutExtension(x.LocalPath ?? string.Empty), video.VideoId, StringComparison.OrdinalIgnoreCase));

                    if (item != null)
                    {
                        item.Status = "成功";
                        item.VideoId = video.VideoId;
                        item.LocalPath = video.LocalPath;
                    }

                    if (!string.IsNullOrWhiteSpace(video.LocalPath))
                    {
                        _lastVideoPath = video.LocalPath;
                        EnsureVideoOption(video.VideoId, video.LocalPath);
                        VideoPathEntry.Text = video.LocalPath;
                    }
                }

                if (!videos.Any())
                {
                    Log("下载结果为空，可能任务被取消");
                    SetDownloadProgress(0, "未下载任何文件");
                }
                else
                {
                    SetDownloadProgress(1, "下载完成");
                    Log("下载任务完成");
                }
            });
        }
        catch (Exception ex)
        {
            Log($"下载异常: {ex.Message}", ex);
        }
    }

    private void EnsureVideoOption(string videoId, string path)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_videoOptions.Any(v => string.Equals(v.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            _videoOptions.Add(new VideoOption { Display = $"{videoId} - {Path.GetFileName(path)}", Path = path });
            if (VideoPicker.SelectedIndex < 0) VideoPicker.SelectedItem = _videoOptions.Last();
        });
    }

    #endregion
    #region Frames

    private void OnSyncDownloadsClicked(object sender, EventArgs e)
    {
        try
        {
            foreach (var d in _downloadItems.Where(x => !string.IsNullOrWhiteSpace(x.LocalPath)))
            {
                EnsureVideoOption(d.VideoId ?? Path.GetFileNameWithoutExtension(d.LocalPath!), d.LocalPath!);
            }

            if (_videoOptions.Any() && string.IsNullOrWhiteSpace(VideoPathEntry.Text))
            {
                VideoPathEntry.Text = _videoOptions.Last().Path;
            }
        }
        catch (Exception ex)
        {
            Log($"同步下载列表失败: {ex.Message}", ex);
        }
    }

    private async void OnBrowseVideoClicked(object sender, EventArgs e)
    {
        try
        {
            var videoTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp4", ".mkv", ".mov" } },
                { DevicePlatform.Android, new[] { "video/*" } },
                { DevicePlatform.iOS, new[] { "public.movie" } },
                { DevicePlatform.MacCatalyst, new[] { "public.movie" } }
            });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择视频文件",
                FileTypes = videoTypes
            });
            if (result != null)
            {
                VideoPathEntry.Text = result.FullPath;
                _lastVideoPath = result.FullPath;
            }
        }
        catch (Exception ex)
        {
            Log($"选择视频失败: {ex.Message}", ex);
        }
    }

    private async void OnBrowseFramesFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var folder = await FolderPickerService.PickFolderAsync("选择帧输出目录");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _frameOutputPath = folder;
                FrameOutputEntry.Text = folder;
            }
        }
        catch (Exception ex)
        {
            Log($"选择帧输出目录失败: {ex.Message}", ex);
        }
    }

    private FrameExtractConfig BuildFrameConfigFromInputs()
    {
        var config = new FrameExtractConfig
        {
            OutputDirectory = _frameOutputPath,
            TargetFps = ParseDouble(TargetFpsEntry.Text, _frameConfig.TargetFps),
            OutputFormat = string.IsNullOrWhiteSpace(OutputFormatEntry.Text) ? "jpg" : OutputFormatEntry.Text!.Trim(),
            Start = ParseTimeSpan(StartTimeEntry.Text),
            End = ParseTimeSpan(EndTimeEntry.Text),
            ResizeWidth = ParseInt(ResizeWidthEntry.Text),
            ResizeHeight = ParseInt(ResizeHeightEntry.Text),
            CropRect = BuildCropRectFromEntries()
        };

        return config;
    }

    private CropRect? BuildCropRectFromEntries()
    {
        var x = ParseInt(RoiXEntry.Text);
        var y = ParseInt(RoiYEntry.Text);
        var w = ParseInt(RoiWidthEntry.Text);
        var h = ParseInt(RoiHeightEntry.Text);
        if (x.HasValue && y.HasValue && w.HasValue && h.HasValue && w.Value > 0 && h.Value > 0)
        {
            return new CropRect(x.Value, y.Value, w.Value, h.Value);
        }
        return null;
    }

    private async void OnPreviewFrameClicked(object sender, EventArgs e)
    {
        var videoPath = GetSelectedVideoPath();
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            Log("请选择有效视频再抓取预览帧");
            return;
        }

        try
        {
            var config = BuildFrameConfigFromInputs();
            var preview = await _extractor.ExtractPreviewFrameAsync(videoPath, config);
            if (!string.IsNullOrWhiteSpace(preview.FilePath) && File.Exists(preview.FilePath))
            {
                _previewFramePath = preview.FilePath;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PreviewImage.Source = ImageSource.FromFile(preview.FilePath);
                });
            }
            else
            {
                Log($"预览失败: {preview.Error ?? "未知错误"}");
            }
        }
        catch (Exception ex)
        {
            Log($"预览异常: {ex.Message}", ex);
        }
    }

    private void OnApplyRoiClicked(object sender, EventArgs e)
    {
        try
        {
            var rect = BuildCropRectFromEntries();
            // ROI 叠加已禁用，保留参数供后端裁剪使用
            if (rect != null) Log("ROI 参数已记录（无叠加显示）");
        }
        catch (Exception ex)
        {
            Log($"应用 ROI 失败: {ex.Message}", ex);
        }
    }

    private string? GetSelectedVideoPath()
    {
        if (VideoPicker.SelectedItem is VideoOption option)
        {
            return option.Path;
        }
        return string.IsNullOrWhiteSpace(VideoPathEntry.Text) ? _lastVideoPath : VideoPathEntry.Text;
    }

    private async void OnExtractClicked(object sender, EventArgs e)
    {
        try
        {
            var videoPath = GetSelectedVideoPath();
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                Log("请先选择有效的视频路径或先执行下载");
                return;
            }

            if (string.IsNullOrWhiteSpace(_frameOutputPath))
            {
                Log("请先选择帧输出目录");
                return;
            }

            try
            {
                Directory.CreateDirectory(_frameOutputPath);
            }
            catch (Exception ex)
            {
                Log($"无法创建帧输出目录：{_frameOutputPath}", ex);
                return;
            }

            var config = BuildFrameConfigFromInputs();

            SetFrameProgress(0, "准备抽帧");

            var progress = new Progress<FrameExtractProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetFrameProgress(0, $"{p.Status}: {p.FramesExtracted}");
                    Log($"{p.Status}: {p.VideoPath} {p.Message}");
                });
            });

            var result = await _extractor.ExtractFramesAsync(videoPath, config, progress);
            _lastFrameDir = result.OutputDirectory;
            _lastFramesForOcr = result.Frames;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FrameDirEntry.Text = result.OutputDirectory;
                if (result.Frames.Count == 0)
                {
                    SetFrameProgress(0, "抽帧失败或无输出");
                    Log("抽帧失败，未生成任何帧");
                }
                else
                {
                    SetFrameProgress(1, $"完成，帧数 {result.Frames.Count}");
                    Log($"抽帧完成，输出目录 {result.OutputDirectory}，帧数 {result.Frames.Count}");
                }
            });
        }
        catch (Exception ex)
        {
            Log($"抽帧异常: {ex.Message}", ex);
        }
    }

    private void SetFrameProgress(double progress, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FrameProgressBar.Progress = Math.Clamp(progress, 0, 1);
            FrameProgressLabel.Text = message;
        });
    }

    #endregion
    #region OCR

    private async void OnBrowseFrameDirClicked(object sender, EventArgs e)
    {
        try
        {
            var folder = await FolderPickerService.PickFolderAsync("选择帧目录");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                FrameDirEntry.Text = folder;
            }
        }
        catch (Exception ex)
        {
            Log($"选择帧目录失败: {ex.Message}", ex);
        }
    }

    private void OnUseLastFramesClicked(object sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_lastFrameDir) && Directory.Exists(_lastFrameDir))
            {
                FrameDirEntry.Text = _lastFrameDir;
            }
            else
            {
                Log("没有可用的最近抽帧目录");
            }
        }
        catch (Exception ex)
        {
            Log($"使用最近抽帧目录失败: {ex.Message}", ex);
        }
    }

    private void OnOcrConfidenceChanged(object sender, ValueChangedEventArgs e)
    {
        try
        {
            OcrConfidenceValueLabel.Text = e.NewValue.ToString("0.00");
        }
        catch (Exception ex)
        {
            Log($"更新置信度显示失败: {ex.Message}", ex);
        }
    }

    private OcrConfig BuildOcrConfigFromInputs()
    {
        var config = new PipelineConfig().Ocr;
        config.Language = OcrLanguagePicker.SelectedItem?.ToString() ?? "zh-CN";
        config.ConfidenceThreshold = OcrConfidenceSlider.Value;
        config.EnableDeduplication = DedupSwitch.IsToggled;
        config.DeduplicationWindowSeconds = ParseDouble(DedupWindowEntry.Text, 1.0);
        return config;
    }

    private List<FrameInfo> LoadFrames(string frameDir)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };
        if (!Directory.Exists(frameDir)) return new List<FrameInfo>();

        return Directory.GetFiles(frameDir)
            .Where(p => allowed.Contains(Path.GetExtension(p)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select((p, idx) => new FrameInfo
            {
                VideoId = new DirectoryInfo(frameDir).Name,
                FrameIndex = idx,
                Timestamp = TimeSpan.FromSeconds(idx),
                ImagePath = p
            })
            .ToList();
    }

    private async void OnOcrClicked(object sender, EventArgs e)
    {
        try
        {
            var frameDir = string.IsNullOrWhiteSpace(FrameDirEntry.Text) ? _lastFrameDir : FrameDirEntry.Text;
            if (string.IsNullOrWhiteSpace(frameDir) || !Directory.Exists(frameDir))
            {
                Log("请先填写有效的帧目录或先执行抽帧");
                return;
            }

            var frames = LoadFrames(frameDir);
            _lastFramesForOcr = frames;
            if (frames.Count == 0)
            {
                Log("帧目录为空");
                return;
            }

            SetOcrProgress(0, "开始 OCR");

            var config = BuildOcrConfigFromInputs();
            var progress = new Progress<string>(msg =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetOcrProgress(0, msg);
                    Log(msg);
                });
            });

            var results = await _ocr.RunAsync(frames, config, progress);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _ocrResults.Clear();
                foreach (var r in results)
                {
                    _ocrResults.Add(r);
                }

                SetOcrProgress(1, $"完成，结果 {results.Count}");

                if (results.Count == 0)
                {
                    Log("未识别到文本");
                }
            });
        }
        catch (Exception ex)
        {
            Log($"OCR 异常: {ex.Message}", ex);
        }
    }

    private void SetOcrProgress(double progress, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OcrProgressBar.Progress = Math.Clamp(progress, 0, 1);
            OcrProgressLabel.Text = message;
        });
    }

    private void OnOcrResultSelected(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is not OcrResult selected)
            {
                return;
            }

            var frame = _lastFramesForOcr.FirstOrDefault(f => f.VideoId == selected.VideoId && f.FrameIndex == selected.FrameIndex);
            if (frame != null && File.Exists(frame.ImagePath))
            {
                OcrPreviewImage.Source = ImageSource.FromFile(frame.ImagePath);
            }
            else
            {
                Log("未找到对应的预览帧文件");
            }
        }
        catch (Exception ex)
        {
            Log($"选择 OCR 结果失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region Utilities

    private static double ParseDouble(string? text, double defaultValue = 0)
    {
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    private static int? ParseInt(string? text)
    {
        return int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static TimeSpan? ParseTimeSpan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }
        return null;
    }

    private void OnOpenLogFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var path = FileSystem.AppDataDirectory;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log($"打开日志目录失败: {ex.Message}", ex);
        }
    }

    #endregion
}
