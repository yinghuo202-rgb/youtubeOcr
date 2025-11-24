using System.Text;
using System.Text.Json;
using YoutubeOcr.Core.Models;

namespace YoutubeOcr.Core.Services;

public interface IResultExporter
{
    Task<string> ExportCsvAsync(IEnumerable<OcrResult> results, string outputPath, CancellationToken cancellationToken = default);
    Task<string> ExportJsonAsync(IEnumerable<OcrResult> results, string outputPath, CancellationToken cancellationToken = default);
    Task<string> ExportCleanCsvAsync(IEnumerable<CleanResult> results, string outputPath, bool includeRawText, CancellationToken cancellationToken = default);
    Task<string> ExportCleanJsonAsync(IEnumerable<CleanResult> results, string outputPath, bool includeRawText, CancellationToken cancellationToken = default);
}

public class ResultExporter : IResultExporter
{
    public async Task<string> ExportCsvAsync(IEnumerable<OcrResult> results, string outputPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var sb = new StringBuilder();
        sb.AppendLine("VideoId,FrameIndex,Timestamp,Text,Confidence,X,Y,Width,Height");
        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var box = result.BoundingBox;
            var line = string.Join(",",
                Escape(result.VideoId),
                result.FrameIndex,
                result.Timestamp.TotalSeconds.ToString("F3"),
                Escape(result.Text.Replace(Environment.NewLine, " ")),
                result.Confidence.ToString("F3"),
                box?.X ?? 0,
                box?.Y ?? 0,
                box?.Width ?? 0,
                box?.Height ?? 0);
            sb.AppendLine(line);
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, cancellationToken);
        return outputPath;
    }

    public async Task<string> ExportCleanCsvAsync(IEnumerable<CleanResult> results, string outputPath, bool includeRawText, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var sb = new StringBuilder();
        var header = includeRawText
            ? "VideoId,FrameIndex,Timestamp,RawText,CleanText,Confidence,X,Y,Width,Height"
            : "VideoId,FrameIndex,Timestamp,CleanText,Confidence,X,Y,Width,Height";
        sb.AppendLine(header);

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var box = result.BoundingBox;
            var fields = new List<string>
            {
                Escape(result.VideoId),
                result.FrameIndex.ToString(),
                result.Timestamp.TotalSeconds.ToString("F3")
            };
            if (includeRawText)
            {
                fields.Add(Escape((result.RawText ?? string.Empty).Replace(Environment.NewLine, " ")));
            }
            fields.Add(Escape((result.CleanText ?? string.Empty).Replace(Environment.NewLine, " ")));
            fields.Add(result.Confidence.ToString("F3"));
            fields.Add((box?.X ?? 0).ToString());
            fields.Add((box?.Y ?? 0).ToString());
            fields.Add((box?.Width ?? 0).ToString());
            fields.Add((box?.Height ?? 0).ToString());

            sb.AppendLine(string.Join(",", fields));
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8, cancellationToken);
        return outputPath;
    }

    public async Task<string> ExportJsonAsync(IEnumerable<OcrResult> results, string outputPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, results, options, cancellationToken);
        return outputPath;
    }

    public async Task<string> ExportCleanJsonAsync(IEnumerable<CleanResult> results, string outputPath, bool includeRawText, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        if (includeRawText)
        {
            await JsonSerializer.SerializeAsync(stream, results.ToList(), options, cancellationToken);
        }
        else
        {
            var payload = results.Select(r => new
            {
                r.VideoId,
                r.FrameIndex,
                r.Timestamp,
                CleanText = r.CleanText,
                r.Confidence,
                BoundingBox = r.BoundingBox
            }).ToList();
            await JsonSerializer.SerializeAsync(stream, payload, options, cancellationToken);
        }
        return outputPath;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
