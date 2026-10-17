namespace ProcessNetMonitor.Models;

public class ProcessInfo
{
    public string Name { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public int ParentProcessId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMB { get; set; }
    public long WorkingSet { get; set; }
    public long PrivateBytes { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public DateTime StartTime { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public List<int> TcpPorts { get; set; } = new();
    public List<int> UdpPorts { get; set; } = new();
    public string PortsString => string.Join(", ", TcpPorts.Concat(UdpPorts).Distinct().OrderBy(p => p));
    public bool IsCriticalSystem { get; set; }
    public string PriorityClass { get; set; } = string.Empty;
}
