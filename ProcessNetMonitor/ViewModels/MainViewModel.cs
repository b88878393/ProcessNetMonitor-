using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProcessNetMonitor.Core;
using ProcessNetMonitor.Models;
using ProcessNetMonitor.Services;

namespace ProcessNetMonitor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ProcessService _processService;
    private readonly NetworkMonitorService _networkMonitor;
    private readonly DispatcherTimer _refreshTimer;
    
    private bool _autoRefresh = true;
    private int _refreshInterval = 2000;
    private string _processFilter = string.Empty;
    private string _networkFilter = string.Empty;
    private bool _showTreeView;
    private bool _isDarkTheme = true;
    private string _statusText = "Ready";
    private ProcessInfo? _selectedProcess;
    private bool _networkLogPaused;
    private long _totalBytesSent;
    private long _totalBytesReceived;
    private string _totalUploadRate = "0 B/s";
    private string _totalDownloadRate = "0 B/s";
    
    public ObservableCollection<ProcessInfo> Processes { get; } = new();
    public ObservableCollection<ProcessInfo> FilteredProcesses { get; } = new();
    public ObservableCollection<NetworkEvent> NetworkEvents { get; } = new();
    public ObservableCollection<NetworkEvent> FilteredNetworkEvents { get; } = new();
    public ObservableCollection<NetworkTrafficData> NetworkTrafficData { get; } = new();
    
    public MainViewModel()
    {
        _processService = new ProcessService();
        _networkMonitor = new NetworkMonitorService();
        
        _networkMonitor.NetworkEventReceived += OnNetworkEventReceived;
        _networkMonitor.StatusChanged += OnNetworkStatusChanged;
        
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_refreshInterval)
        };
        _refreshTimer.Tick += (s, e) => RefreshProcesses();
        
        InitializeCommands();
        
        if (AutoRefresh)
            _refreshTimer.Start();
        
        RefreshProcesses();
    }
    
    private void InitializeCommands()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        ClearFilterCommand = new RelayCommand(_ => ProcessFilter = string.Empty);
        KillProcessCommand = new RelayCommand(_ => KillSelectedProcess(), _ => SelectedProcess != null);
        KillTreeCommand = new RelayCommand(_ => KillSelectedProcessTree(), _ => SelectedProcess != null);
        KillAllSameNameCommand = new RelayCommand(_ => KillAllSameName(), _ => SelectedProcess != null);
        SuspendProcessCommand = new RelayCommand(_ => SuspendSelectedProcess(), _ => SelectedProcess != null);
        ResumeProcessCommand = new RelayCommand(_ => ResumeSelectedProcess(), _ => SelectedProcess != null);
        SetPriorityCommand = new RelayCommand(priority => SetSelectedProcessPriority(priority?.ToString() ?? "Normal"));
        OpenFileLocationCommand = new RelayCommand(_ => OpenSelectedProcessLocation(), _ => SelectedProcess != null);
        CopyDetailsCommand = new RelayCommand(_ => CopySelectedProcessDetails(), _ => SelectedProcess != null);
        ShowPropertiesCommand = new RelayCommand(_ => ShowSelectedProcessProperties(), _ => SelectedProcess != null);
        
        StartNetworkMonitoringCommand = new RelayCommand(_ => StartNetworkMonitoring());
        StopNetworkMonitoringCommand = new RelayCommand(_ => StopNetworkMonitoring());
        ClearNetworkLogCommand = new RelayCommand(_ => ClearNetworkLog());
        ClearNetworkFilterCommand = new RelayCommand(_ => NetworkFilter = string.Empty);
        ExportNetworkLogCommand = new RelayCommand(_ => ExportNetworkLog());
        
        RestartAsAdminCommand = new RelayCommand(_ => RestartAsAdmin());
        SetRefreshIntervalCommand = new RelayCommand(interval => SetRefreshInterval(int.Parse(interval?.ToString() ?? "2000")));
        AboutCommand = new RelayCommand(_ => ShowAbout());
        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
    }
    
    private void OnNetworkEventReceived(object? sender, NetworkEvent e)
    {
        if (_networkLogPaused)
            return;
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            NetworkEvents.Insert(0, e);
            
            if (NetworkEvents.Count > 10000)
                NetworkEvents.RemoveAt(NetworkEvents.Count - 1);
            
            ApplyNetworkFilter();
            UpdateNetworkTraffic(e);
        });
    }
    
    private void OnNetworkStatusChanged(object? sender, string status)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = status;
        });
    }
    
    private void UpdateNetworkTraffic(NetworkEvent evt)
    {
        var traffic = NetworkTrafficData.FirstOrDefault(t => t.ProcessId == evt.ProcessId);
        if (traffic == null)
        {
            traffic = new NetworkTrafficData
            {
                ProcessName = evt.ProcessName,
                ProcessId = evt.ProcessId
            };
            NetworkTrafficData.Add(traffic);
        }
        
        if (evt.Direction == "Send")
        {
            traffic.TotalBytesSent += evt.Size;
            traffic.UploadRate = ByteFormatter.FormatRate(evt.Size);
            _totalBytesSent += evt.Size;
            TotalUploadRate = ByteFormatter.FormatRate(evt.Size);
        }
        else
        {
            traffic.TotalBytesReceived += evt.Size;
            traffic.DownloadRate = ByteFormatter.FormatRate(evt.Size);
            _totalBytesReceived += evt.Size;
            TotalDownloadRate = ByteFormatter.FormatRate(evt.Size);
        }

        OnPropertyChanged(nameof(TotalBytesSentDisplay));
        OnPropertyChanged(nameof(TotalBytesReceivedDisplay));
    }
    
    private void RefreshProcesses()
    {
        try
        {
            var processes = _processService.GetAllProcesses();
            
            Processes.Clear();
            foreach (var process in processes)
            {
                Processes.Add(process);
            }
            
            ApplyProcessFilter();
            OnPropertyChanged(nameof(ProcessCount));
            OnPropertyChanged(nameof(IsAdministrator));
        }
        catch (Exception ex)
        {
            StatusText = $"Error refreshing processes: {ex.Message}";
        }
    }
    
    private void ApplyProcessFilter()
    {
        FilteredProcesses.Clear();

        var parsed = ProcessFilterParser.Parse(ProcessFilter);
        IEnumerable<ProcessInfo> filtered = Processes.Where(p =>
            ProcessFilterParser.Matches(
                parsed,
                p.Name,
                p.ProcessId,
                p.ExecutablePath,
                p.CommandLine,
                p.TcpPorts.Concat(p.UdpPorts)));

        var list = filtered.ToList();
        foreach (var p in list)
            p.TreeDepth = 0;

        if (ShowTreeView)
        {
            var byParent = list.GroupBy(p => p.ParentProcessId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).ToList());
            var pidSet = list.Select(p => p.ProcessId).ToHashSet();
            var roots = list.Where(p => p.ParentProcessId == 0 || !pidSet.Contains(p.ParentProcessId))
                .OrderBy(p => p.Name)
                .ToList();

            var ordered = new List<ProcessInfo>();
            var visited = new HashSet<int>();

            void Walk(ProcessInfo node, int depth)
            {
                if (!visited.Add(node.ProcessId))
                    return;
                node.TreeDepth = depth;
                ordered.Add(node);
                if (byParent.TryGetValue(node.ProcessId, out var kids))
                {
                    foreach (var kid in kids)
                        Walk(kid, depth + 1);
                }
            }

            foreach (var root in roots)
                Walk(root, 0);

            foreach (var p in list.Where(p => !visited.Contains(p.ProcessId)).OrderBy(p => p.Name))
                Walk(p, 0);

            list = ordered;
        }
        else
        {
            list = list.OrderBy(p => p.Name).ToList();
        }

        foreach (var process in list)
            FilteredProcesses.Add(process);
    }
    
    private void ApplyNetworkFilter()
    {
        FilteredNetworkEvents.Clear();
        
        var filtered = NetworkEvents.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(NetworkFilter))
        {
            var filter = NetworkFilter.ToLower();
            filtered = filtered.Where(e =>
                e.ProcessName.ToLower().Contains(filter) ||
                e.LocalAddress.Contains(filter) ||
                e.RemoteAddress.Contains(filter) ||
                e.Protocol.ToLower().Contains(filter) ||
                e.RemoteHost.ToLower().Contains(filter));
        }
        
        foreach (var evt in filtered.Take(5000))
        {
            FilteredNetworkEvents.Add(evt);
        }
    }
    
    private void KillSelectedProcess()
    {
        if (SelectedProcess == null) return;
        
        if (SelectedProcess.IsCriticalSystem)
        {
            var result = MessageBox.Show(
                $"'{SelectedProcess.Name}' is a critical system process. Killing it may cause system instability.\n\nAre you sure you want to continue?",
                "Warning - Critical System Process",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;
        }
        
        if (_processService.KillProcess(SelectedProcess.ProcessId))
        {
            StatusText = $"Killed process {SelectedProcess.Name} (PID: {SelectedProcess.ProcessId})";
            RefreshProcesses();
        }
        else
        {
            MessageBox.Show($"Failed to kill process. You may need administrator privileges.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void KillSelectedProcessTree()
    {
        if (SelectedProcess == null) return;
        
        var result = MessageBox.Show(
            $"This will kill '{SelectedProcess.Name}' and all its child processes.\n\nAre you sure?",
            "Confirm Kill Process Tree",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            if (_processService.KillProcessTree(SelectedProcess.ProcessId))
            {
                StatusText = $"Killed process tree for {SelectedProcess.Name}";
                RefreshProcesses();
            }
            else
            {
                MessageBox.Show("Failed to kill process tree.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void KillAllSameName()
    {
        if (SelectedProcess == null) return;
        
        var result = MessageBox.Show(
            $"This will kill ALL processes named '{SelectedProcess.Name}'.\n\nAre you sure?",
            "Confirm Kill All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            var sameName = Processes.Where(p => p.Name == SelectedProcess.Name).ToList();
            int killed = 0;
            
            foreach (var process in sameName)
            {
                if (_processService.KillProcess(process.ProcessId))
                    killed++;
            }
            
            StatusText = $"Killed {killed} processes named '{SelectedProcess.Name}'";
            RefreshProcesses();
        }
    }
    
    private void SuspendSelectedProcess()
    {
        if (SelectedProcess == null) return;
        
        if (_processService.SuspendProcess(SelectedProcess.ProcessId))
        {
            StatusText = $"Suspended process {SelectedProcess.Name}";
        }
        else
        {
            MessageBox.Show("Failed to suspend process. You may need administrator privileges.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ResumeSelectedProcess()
    {
        if (SelectedProcess == null) return;
        
        if (_processService.ResumeProcess(SelectedProcess.ProcessId))
        {
            StatusText = $"Resumed process {SelectedProcess.Name}";
        }
        else
        {
            MessageBox.Show("Failed to resume process.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void SetSelectedProcessPriority(string priority)
    {
        if (SelectedProcess == null) return;
        
        if (Enum.TryParse<ProcessPriorityClass>(priority, out var priorityClass))
        {
            if (_processService.SetProcessPriority(SelectedProcess.ProcessId, priorityClass))
            {
                StatusText = $"Set priority of {SelectedProcess.Name} to {priority}";
            }
            else
            {
                MessageBox.Show("Failed to set process priority.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void OpenSelectedProcessLocation()
    {
        if (SelectedProcess == null || string.IsNullOrEmpty(SelectedProcess.ExecutablePath))
            return;
        
        try
        {
            Process.Start("explorer.exe", $"/select,\"{SelectedProcess.ExecutablePath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open location: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CopySelectedProcessDetails()
    {
        if (SelectedProcess == null) return;
        
        var details = $"Name: {SelectedProcess.Name}\n" +
                     $"PID: {SelectedProcess.ProcessId}\n" +
                     $"Parent PID: {SelectedProcess.ParentProcessId}\n" +
                     $"User: {SelectedProcess.UserName}\n" +
                     $"Path: {SelectedProcess.ExecutablePath}\n" +
                     $"Command Line: {SelectedProcess.CommandLine}\n" +
                     $"Memory: {SelectedProcess.MemoryMB:N2} MB\n" +
                     $"CPU: {SelectedProcess.CpuPercent}%\n" +
                     $"Threads: {SelectedProcess.ThreadCount}\n" +
                     $"Handles: {SelectedProcess.HandleCount}\n" +
                     $"Started: {SelectedProcess.StartTime}\n" +
                     $"Ports: {SelectedProcess.PortsString}";
        
        Clipboard.SetText(details);
        StatusText = "Process details copied to clipboard";
    }
    
    private void ShowSelectedProcessProperties()
    {
        if (SelectedProcess == null) return;
        
        var details = $"Process Details\n\n" +
                     $"Name: {SelectedProcess.Name}\n" +
                     $"PID: {SelectedProcess.ProcessId}\n" +
                     $"Parent PID: {SelectedProcess.ParentProcessId}\n" +
                     $"User: {SelectedProcess.UserName}\n" +
                     $"Path: {SelectedProcess.ExecutablePath}\n" +
                     $"Command Line: {SelectedProcess.CommandLine}\n" +
                     $"Company: {SelectedProcess.Company}\n" +
                     $"Memory (Working Set): {SelectedProcess.MemoryMB:N2} MB\n" +
                     $"Memory (Private): {SelectedProcess.PrivateBytes / 1024.0 / 1024.0:N2} MB\n" +
                     $"CPU: {SelectedProcess.CpuPercent}%\n" +
                     $"Threads: {SelectedProcess.ThreadCount}\n" +
                     $"Handles: {SelectedProcess.HandleCount}\n" +
                     $"Priority: {SelectedProcess.PriorityClass}\n" +
                     $"Started: {SelectedProcess.StartTime}\n" +
                     $"TCP Ports: {string.Join(", ", SelectedProcess.TcpPorts)}\n" +
                     $"UDP Ports: {string.Join(", ", SelectedProcess.UdpPorts)}\n" +
                     $"Critical System Process: {SelectedProcess.IsCriticalSystem}";
        
        MessageBox.Show(details, "Process Properties", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    private void StartNetworkMonitoring()
    {
        if (!ProcessService.IsAdministrator())
        {
            MessageBox.Show("Network monitoring requires administrator privileges.\n\n" +
                          "Please restart the application as administrator.",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        
        if (_networkMonitor.Start())
        {
            StatusText = "Network monitoring started";
        }
        else
        {
            MessageBox.Show("Failed to start network monitoring. Make sure you're running as administrator.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void StopNetworkMonitoring()
    {
        _networkMonitor.Stop();
        StatusText = "Network monitoring stopped";
    }
    
    private void ClearNetworkLog()
    {
        NetworkEvents.Clear();
        FilteredNetworkEvents.Clear();
        NetworkTrafficData.Clear();
        _totalBytesSent = 0;
        _totalBytesReceived = 0;
        TotalUploadRate = "0 B/s";
        TotalDownloadRate = "0 B/s";
        OnPropertyChanged(nameof(TotalBytesSentDisplay));
        OnPropertyChanged(nameof(TotalBytesReceivedDisplay));
    }
    
    private void ExportNetworkLog()
    {
        try
        {
            var filename = $"network_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
            
            using var writer = new StreamWriter(path);
            writer.WriteLine("Timestamp,Process,PID,Direction,Protocol,LocalAddress,RemoteAddress,RemoteHost,Size,State");
            
            foreach (var evt in NetworkEvents)
            {
                writer.WriteLine($"{evt.Timestamp:O},{evt.ProcessName},{evt.ProcessId},{evt.Direction}," +
                               $"{evt.Protocol},{evt.LocalAddress},{evt.RemoteAddress},{evt.RemoteHost}," +
                               $"{evt.Size},{evt.State}");
            }
            
            StatusText = $"Exported to {filename}";
            MessageBox.Show($"Network log exported to:\n{path}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void RestartAsAdmin()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };
            
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch
        {
            MessageBox.Show("Failed to restart as administrator.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void SetRefreshInterval(int milliseconds)
    {
        RefreshInterval = milliseconds;
        StatusText = $"Refresh interval set to {milliseconds}ms";
    }
    
    private void ShowAbout()
    {
        MessageBox.Show(
            "Process & Network Monitor v1.0\n\n" +
            "A comprehensive process and network monitoring tool.\n\n" +
            "Features:\n" +
            "• Advanced process management\n" +
            "• Real-time network traffic monitoring with ETW\n" +
            "• Port to process mapping\n" +
            "• Process suspend/resume, priority control\n" +
            "• Network event logging and export\n\n" +
            "Built with .NET 8 and WPF\n" +
            "Requires Windows 10/11 x64\n" +
            "Administrator privileges required for network monitoring",
            "About Process & Network Monitor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    // Properties
    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (_autoRefresh != value)
            {
                _autoRefresh = value;
                OnPropertyChanged();
                
                if (value)
                    _refreshTimer.Start();
                else
                    _refreshTimer.Stop();
            }
        }
    }
    
    public int RefreshInterval
    {
        get => _refreshInterval;
        set
        {
            if (_refreshInterval != value)
            {
                _refreshInterval = value;
                _refreshTimer.Interval = TimeSpan.FromMilliseconds(value);
                OnPropertyChanged();
            }
        }
    }
    
    public string ProcessFilter
    {
        get => _processFilter;
        set
        {
            if (_processFilter != value)
            {
                _processFilter = value;
                OnPropertyChanged();
                ApplyProcessFilter();
            }
        }
    }
    
    public string NetworkFilter
    {
        get => _networkFilter;
        set
        {
            if (_networkFilter != value)
            {
                _networkFilter = value;
                OnPropertyChanged();
                ApplyNetworkFilter();
            }
        }
    }
    
    public bool ShowTreeView
    {
        get => _showTreeView;
        set
        {
            if (_showTreeView != value)
            {
                _showTreeView = value;
                OnPropertyChanged();
                ApplyProcessFilter();
            }
        }
    }

    public string TotalUploadRate
    {
        get => _totalUploadRate;
        set { if (_totalUploadRate != value) { _totalUploadRate = value; OnPropertyChanged(); } }
    }

    public string TotalDownloadRate
    {
        get => _totalDownloadRate;
        set { if (_totalDownloadRate != value) { _totalDownloadRate = value; OnPropertyChanged(); } }
    }

    public string TotalBytesSentDisplay => ByteFormatter.FormatBytes(_totalBytesSent);
    public string TotalBytesReceivedDisplay => ByteFormatter.FormatBytes(_totalBytesReceived);
    public string NpcapStatus => NpcapDetector.StatusMessage;
    
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (_isDarkTheme != value)
            {
                _isDarkTheme = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }
    
    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (_selectedProcess != value)
            {
                _selectedProcess = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool NetworkLogPaused
    {
        get => _networkLogPaused;
        set
        {
            if (_networkLogPaused != value)
            {
                _networkLogPaused = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string NetworkStatus => _networkMonitor.IsRunning ? "Monitoring..." : "Stopped";
    public bool IsAdministrator => ProcessService.IsAdministrator();
    public int ProcessCount => Processes.Count;
    
    // Commands
    public ICommand RefreshProcessesCommand { get; private set; } = null!;
    public ICommand ClearFilterCommand { get; private set; } = null!;
    public ICommand KillProcessCommand { get; private set; } = null!;
    public ICommand KillTreeCommand { get; private set; } = null!;
    public ICommand KillAllSameNameCommand { get; private set; } = null!;
    public ICommand SuspendProcessCommand { get; private set; } = null!;
    public ICommand ResumeProcessCommand { get; private set; } = null!;
    public ICommand SetPriorityCommand { get; private set; } = null!;
    public ICommand OpenFileLocationCommand { get; private set; } = null!;
    public ICommand CopyDetailsCommand { get; private set; } = null!;
    public ICommand ShowPropertiesCommand { get; private set; } = null!;
    public ICommand StartNetworkMonitoringCommand { get; private set; } = null!;
    public ICommand StopNetworkMonitoringCommand { get; private set; } = null!;
    public ICommand ClearNetworkLogCommand { get; private set; } = null!;
    public ICommand ClearNetworkFilterCommand { get; private set; } = null!;
    public ICommand ExportNetworkLogCommand { get; private set; } = null!;
    public ICommand RestartAsAdminCommand { get; private set; } = null!;
    public ICommand SetRefreshIntervalCommand { get; private set; } = null!;
    public ICommand AboutCommand { get; private set; } = null!;
    public ICommand ExitCommand { get; private set; } = null!;
    
    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Simple RelayCommand implementation
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;
    
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
    
    public void Execute(object? parameter) => _execute(parameter);
    
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
