using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using ProcessNetMonitor.Models;

namespace ProcessNetMonitor.Services;

public class NetworkMonitorService : IDisposable
{
    private TraceEventSession? _session;
    private Task? _monitoringTask;
    private CancellationTokenSource? _cancellationSource;
    private readonly object _lock = new();
    private bool _isRunning;
    
    public event EventHandler<NetworkEvent>? NetworkEventReceived;
    public event EventHandler<string>? StatusChanged;
    
    private readonly Dictionary<int, string> _processNames = new();
    private readonly Dictionary<string, string> _hostCache = new();
    
    public bool IsRunning => _isRunning;
    
    public bool Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return false;
            
            try
            {
                _cancellationSource = new CancellationTokenSource();
                _monitoringTask = Task.Run(() => MonitorNetworkTraffic(_cancellationSource.Token));
                _isRunning = true;
                StatusChanged?.Invoke(this, "Monitoring...");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Failed to start: {ex.Message}");
                return false;
            }
        }
    }
    
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;
            
            _cancellationSource?.Cancel();
            _session?.Stop();
            _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
            _session?.Dispose();
            _session = null;
            _isRunning = false;
            StatusChanged?.Invoke(this, "Stopped");
        }
    }
    
    private void MonitorNetworkTraffic(CancellationToken cancellationToken)
    {
        try
        {
            var sessionName = "ProcessNetMonitor-" + Guid.NewGuid();
            
            using (_session = new TraceEventSession(sessionName))
            {
                _session.EnableKernelProvider(
                    Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords.NetworkTCPIP);
                
                var parser = _session.Source.Kernel;
                
                parser.TcpIpSend += data =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    try
                    {
                        var evt = new NetworkEvent
                        {
                            Timestamp = data.TimeStamp,
                            ProcessId = data.ProcessID,
                            ProcessName = GetProcessName(data.ProcessID),
                            Direction = "Send",
                            Protocol = "TCP",
                            LocalAddress = $"{data.saddr}:{data.sport}",
                            RemoteAddress = $"{data.daddr}:{data.dport}",
                            RemoteHost = GetHostName(data.daddr.ToString()),
                            Size = data.size,
                            State = "Established"
                        };
                        
                        NetworkEventReceived?.Invoke(this, evt);
                    }
                    catch { }
                };
                
                parser.TcpIpRecv += data =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    try
                    {
                        var evt = new NetworkEvent
                        {
                            Timestamp = data.TimeStamp,
                            ProcessId = data.ProcessID,
                            ProcessName = GetProcessName(data.ProcessID),
                            Direction = "Receive",
                            Protocol = "TCP",
                            LocalAddress = $"{data.daddr}:{data.dport}",
                            RemoteAddress = $"{data.saddr}:{data.sport}",
                            RemoteHost = GetHostName(data.saddr.ToString()),
                            Size = data.size,
                            State = "Established"
                        };
                        
                        NetworkEventReceived?.Invoke(this, evt);
                    }
                    catch { }
                };
                
                parser.UdpIpSend += data =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    try
                    {
                        var evt = new NetworkEvent
                        {
                            Timestamp = data.TimeStamp,
                            ProcessId = data.ProcessID,
                            ProcessName = GetProcessName(data.ProcessID),
                            Direction = "Send",
                            Protocol = "UDP",
                            LocalAddress = $"{data.saddr}:{data.sport}",
                            RemoteAddress = $"{data.daddr}:{data.dport}",
                            RemoteHost = GetHostName(data.daddr.ToString()),
                            Size = data.size,
                            State = "Datagram"
                        };
                        
                        NetworkEventReceived?.Invoke(this, evt);
                    }
                    catch { }
                };
                
                parser.UdpIpRecv += data =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    try
                    {
                        var evt = new NetworkEvent
                        {
                            Timestamp = data.TimeStamp,
                            ProcessId = data.ProcessID,
                            ProcessName = GetProcessName(data.ProcessID),
                            Direction = "Receive",
                            Protocol = "UDP",
                            LocalAddress = $"{data.daddr}:{data.dport}",
                            RemoteAddress = $"{data.saddr}:{data.sport}",
                            RemoteHost = GetHostName(data.saddr.ToString()),
                            Size = data.size,
                            State = "Datagram"
                        };
                        
                        NetworkEventReceived?.Invoke(this, evt);
                    }
                    catch { }
                };
                
                _session.Source.Process();
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Error: {ex.Message}");
        }
    }
    
    private string GetProcessName(int processId)
    {
        if (_processNames.TryGetValue(processId, out var name))
            return name;
        
        try
        {
            var process = Process.GetProcessById(processId);
            name = process.ProcessName;
            _processNames[processId] = name;
            return name;
        }
        catch
        {
            return $"PID:{processId}";
        }
    }
    
    private string GetHostName(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return string.Empty;
        
        if (_hostCache.TryGetValue(ipAddress, out var host))
            return host;
        
        Task.Run(async () =>
        {
            try
            {
                if (IPAddress.TryParse(ipAddress, out var ip))
                {
                    var entry = await Dns.GetHostEntryAsync(ip);
                    _hostCache[ipAddress] = entry.HostName;
                }
            }
            catch
            {
                _hostCache[ipAddress] = string.Empty;
            }
        });
        
        return string.Empty;
    }
    
    public void Dispose()
    {
        Stop();
        _cancellationSource?.Dispose();
    }
}
