using System.Globalization;

namespace YoutubeOcr.Core.Services;

public static class TimeParser
{
    public static TimeSpan? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }
}
