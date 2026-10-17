namespace ProcessNetMonitor.Core;

/// <summary>Pure formatting helpers for rates/sizes (unit-testable on Linux).</summary>
public static class ByteFormatter
{
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = Math.Abs(bytes);
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        var sign = bytes < 0 ? "-" : "";
        return $"{sign}{len:0.##} {sizes[order]}";
    }

    public static string FormatRate(long bytesPerSecond) => $"{FormatBytes(bytesPerSecond)}/s";
}
