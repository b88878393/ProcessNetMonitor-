namespace ProcessNetMonitor.Models;

public class NetworkTrafficData
{
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string UploadRate { get; set; } = "0 B/s";
    public string DownloadRate { get; set; } = "0 B/s";
    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }
}
