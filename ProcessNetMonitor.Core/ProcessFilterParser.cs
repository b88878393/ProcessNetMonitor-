namespace ProcessNetMonitor.Core;

/// <summary>
/// Pure filter-parsing logic (unit-testable on Linux).
/// Supports: free text, pid:1234, :8080, port:8080.
/// </summary>
public static class ProcessFilterParser
{
    public enum FilterKind
    {
        Text,
        Port,
        Pid
    }

    public readonly record struct ParsedFilter(FilterKind Kind, string Text, int? Port, int? Pid);

    public static ParsedFilter Parse(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new ParsedFilter(FilterKind.Text, string.Empty, null, null);

        var trimmed = filter.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower.StartsWith("pid:"))
        {
            var pidStr = trimmed[4..].Trim();
            if (int.TryParse(pidStr, out var pid))
                return new ParsedFilter(FilterKind.Pid, string.Empty, null, pid);
        }

        if (lower.StartsWith("port:"))
        {
            var portStr = trimmed[5..].Trim();
            if (int.TryParse(portStr, out var port))
                return new ParsedFilter(FilterKind.Port, string.Empty, port, null);
        }

        if (lower.StartsWith(':'))
        {
            var portStr = trimmed[1..].Trim();
            if (int.TryParse(portStr, out var port))
                return new ParsedFilter(FilterKind.Port, string.Empty, port, null);
        }

        return new ParsedFilter(FilterKind.Text, lower, null, null);
    }

    public static bool Matches(ParsedFilter filter, string name, int processId, string path, string commandLine, IEnumerable<int> ports)
    {
        return filter.Kind switch
        {
            FilterKind.Pid => filter.Pid.HasValue && processId == filter.Pid.Value,
            FilterKind.Port => filter.Port.HasValue && ports.Contains(filter.Port.Value),
            _ => string.IsNullOrEmpty(filter.Text)
                 || name.Contains(filter.Text, StringComparison.OrdinalIgnoreCase)
                 || processId.ToString().Contains(filter.Text, StringComparison.Ordinal)
                 || path.Contains(filter.Text, StringComparison.OrdinalIgnoreCase)
                 || commandLine.Contains(filter.Text, StringComparison.OrdinalIgnoreCase)
        };
    }
}
