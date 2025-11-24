namespace YoutubeOcr.Core.Services;

public class ToolLocator
{
    private readonly string _toolsDirectory;

    public ToolLocator(string? toolsDirectory = null)
    {
        _toolsDirectory = toolsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Tools");
    }

    public string ResolveYtDlp(string? overridePath = null)
    {
        return ResolveTool(overridePath, "yt-dlp.exe");
    }

    public string ResolveFfmpeg(string? overridePath = null)
    {
        return ResolveTool(overridePath, "ffmpeg.exe");
    }

    private string ResolveTool(string? overridePath, string defaultFileName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                throw new FileNotFoundException($"未找到工具: {overridePath}");
            }

            return overridePath;
        }

        var candidate = Path.Combine(_toolsDirectory, defaultFileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var fallback = FindInParents(defaultFileName);
        if (fallback != null)
        {
            return fallback;
        }

        throw new FileNotFoundException($"请将 {defaultFileName} 放到 Tools 目录，当前查找路径: {candidate}");
    }

    private static string? FindInParents(string defaultFileName, int maxDepth = 5)
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < maxDepth; i++)
        {
            var tools = Path.Combine(current, "Tools", defaultFileName);
            if (File.Exists(tools))
            {
                return tools;
            }

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // also try the process working directory
        var cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), "Tools", defaultFileName);
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }
}
