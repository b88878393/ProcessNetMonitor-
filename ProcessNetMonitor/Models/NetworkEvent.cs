namespace ProcessNetMonitor.Models;

public class NetworkEvent
{
    public DateTime Timestamp { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string LocalAddress { get; set; } = string.Empty;
    public string RemoteAddress { get; set; } = string.Empty;
    public string RemoteHost { get; set; } = string.Empty;
    public int Size { get; set; }
    public string State { get; set; } = string.Empty;
}
