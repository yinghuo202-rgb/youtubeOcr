using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;
using OcrResultModel = YoutubeOcr.Core.Models.OcrResult;

namespace YoutubeOcr.Core.Services;

public interface IOcrEngine
{
    Task<IReadOnlyList<OcrResultModel>> RunAsync(
        IEnumerable<FrameInfo> frames,
        OcrConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public class WindowsOcrEngine : IOcrEngine
{
    public async Task<IReadOnlyList<OcrResultModel>> RunAsync(
        IEnumerable<FrameInfo> frames,
        OcrConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var language = CreateLanguage(config.Language);
        var engine = language is null ? OcrEngine.TryCreateFromUserProfileLanguages() : OcrEngine.TryCreateFromLanguage(language);
        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            FileLogger.LogError(null, "无法初始化 Windows OCR 引擎");
            throw new InvalidOperationException("无法初始化 Windows OCR 引擎");
        }

        var results = new List<OcrResultModel>();

        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(frame.ImagePath))
            {
                continue;
            }

            try
            {
                progress?.Report($"OCR: {Path.GetFileName(frame.ImagePath)}");

                using var stream = await FileRandomAccessStream.OpenAsync(frame.ImagePath, Windows.Storage.FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var bitmap = await decoder.GetSoftwareBitmapAsync();

                var ocr = await engine.RecognizeAsync(bitmap);
                var text = ocr.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var confidence = CalculateConfidence(ocr);
                if (confidence < config.ConfidenceThreshold)
                {
                    continue;
                }

                var firstRect = ocr.Lines.FirstOrDefault()?.Words.FirstOrDefault()?.BoundingRect;
                BoundingBox? box = null;
                if (firstRect != null)
                {
                    box = new BoundingBox(firstRect.Value.X, firstRect.Value.Y, firstRect.Value.Width, firstRect.Value.Height);
                }

                results.Add(new OcrResultModel
                {
                    VideoId = frame.VideoId,
                    FrameIndex = frame.FrameIndex,
                    Timestamp = frame.Timestamp,
                    Text = text.Trim(),
                    Confidence = confidence,
                    BoundingBox = box
                });
            }
            catch (Exception ex)
            {
                FileLogger.LogError(ex, $"OCR error on frame {frame.ImagePath}");
            }
        }

        if (config.EnableDeduplication)
        {
            results = Deduplicate(results, config.DeduplicationWindowSeconds);
        }

        return results;
    }

    private static Language? CreateLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return null;
        try
        {
            return new Language(lang);
        }
        catch
        {
            return null;
        }
    }

    private static double CalculateConfidence(Windows.Media.Ocr.OcrResult result)
    {
        // Windows.Media.Ocr does not expose per-word confidence; treat presence of text as high confidence.
        return result.Text.Length > 0 ? 1.0 : 0.0;
    }

    private static List<OcrResultModel> Deduplicate(IEnumerable<OcrResultModel> results, double windowSeconds)
    {
        var sorted = results.OrderBy(r => r.Timestamp).ToList();
        var deduped = new List<OcrResultModel>();
        foreach (var item in sorted)
        {
            var duplicate = deduped.LastOrDefault(r =>
                r.VideoId == item.VideoId &&
                Math.Abs((r.Timestamp - item.Timestamp).TotalSeconds) <= windowSeconds &&
                string.Equals(r.Text, item.Text, StringComparison.OrdinalIgnoreCase));

            if (duplicate == null)
            {
                deduped.Add(item);
            }
        }

        return deduped;
    }
}
