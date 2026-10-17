# Process & Network Monitor

A comprehensive, portable Windows desktop application for advanced process management and real-time network traffic monitoring. Built with C# .NET 8 and WPF.

## Features

### Process Management Tab
- **Complete Process List**: View all running processes with detailed information
  - Name, PID, Parent PID, User, CPU %, Memory (Working Set/Private)
  - Thread count, Handle count, Start time
  - Full executable path, Command line arguments
  - Company/Signer information

- **Network Port Mapping**: See which TCP/UDP ports each process is using
  - Utilizes Windows `GetExtendedTcpTable` and `GetExtendedUdpTable` APIs
  - Maps both IPv4 and IPv6 connections to processes

- **Advanced Search/Filter**:
  - Search by process name, PID, path, command line
  - Filter by port: type `:8080` or `port:8080` to find processes using that port
  - Filter by PID: type `pid:1234`

- **Process Operations**:
  - Kill process, Kill process tree, Kill all processes with same name
  - Suspend/Resume process (using NT APIs)
  - Set process priority (Realtime, High, Above Normal, Normal, Below Normal, Idle)
  - Open file location in Explorer
  - Copy process details to clipboard
  - View detailed process properties

- **Safety Features**:
  - Identifies and warns about critical system processes (System, csrss, lsass, etc.)
  - Confirmation dialogs for destructive operations
  - Gracefully handles access-denied scenarios
  - "Restart as Administrator" option

- **Performance**:
  - Sortable columns
  - Tree view toggle (parent/child relationships)
  - Configurable auto-refresh (500ms to 5s intervals)
  - Low CPU overhead

### Network Traffic Tab
- **Real-Time Network Monitoring**:
  - Per-process network activity tracking
  - Uses ETW (Event Tracing for Windows) with Microsoft-Windows-Kernel-Network provider
  - Requires administrator privileges (no driver installation needed)

- **Traffic Information**:
  - Process name, PID, Direction (Send/Receive)
  - Protocol (TCP/UDP), Local/Remote address:port
  - Bytes sent/received, Connection state
  - Reverse DNS hostname lookup (async, cached)

- **Live Traffic Chart**:
  - Real-time upload/download throughput per process
  - Visual representation of network activity

- **Event Log** (Wireshark-style):
  - Timestamp, Process, Direction, Protocol, Addresses, Size
  - Filter by process, port, IP, protocol
  - Pause/Resume logging
  - Export to CSV for analysis
  - Maintains up to 10,000 events in memory

## Technical Details

### Built With
- **.NET 8** (net8.0-windows)
- **WPF** (Windows Presentation Foundation)
- **TraceEvent** library for ETW network monitoring
- **P/Invoke** for Windows API access (iphlpapi.dll, ntdll.dll, kernel32.dll)

### Architecture
- **MVVM Pattern** (Model-View-ViewModel)
- Clean separation of concerns:
  - `Models/`: Data structures (ProcessInfo, NetworkEvent, NetworkTrafficData)
  - `ViewModels/`: UI logic and data binding (MainViewModel)
  - `Services/`: Business logic (ProcessService, NetworkMonitorService)
  - `Native/`: P/Invoke declarations (NativeMethods)
  - `Styles/`: UI theming (DarkTheme.xaml)

### Portability
- **Single-File Executable**: Self-contained, no installation required
- Configuration: `PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`
- Settings stored in application directory or %AppData%
- No external dependencies or drivers (TraceEvent included)

## Requirements

- **OS**: Windows 10 or Windows 11 (x64)
- **Privileges**: 
  - Process monitoring works without admin
  - Network monitoring requires Administrator privileges (for ETW)
- **Optional**: Npcap (for future packet-level capture features - not required for current functionality)

## Building from Source

### Prerequisites
- .NET 8 SDK

### Build Instructions

#### On Windows:
```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained
```

#### Cross-Compile from Linux/macOS:
```bash
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained
```

The compiled executable will be in:
```
bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe
```

### Build Configuration
The project is configured with:
- `EnableWindowsTargeting=true` - Allows building Windows apps on Linux/macOS
- `PublishSingleFile=true` - Creates a single executable
- `SelfContained=true` - Includes .NET runtime
- `RuntimeIdentifier=win-x64` - Targets 64-bit Windows

## Usage

### Starting the Application
1. Run `ProcessNetMonitor.exe`
2. The application runs with normal user privileges by default
3. For network monitoring, click "File → Restart as Administrator"

### Process Monitoring
1. The **Processes** tab shows all running processes automatically
2. Use the filter box to search:
   - Type process name: `chrome`
   - Search by port: `:443` or `port:443`
   - Search by PID: `pid:1234`
3. Right-click any process for operations menu
4. Use toolbar buttons for quick actions

### Network Monitoring
1. Switch to **Network Traffic** tab
2. Click "Start Monitoring" (requires admin)
3. Watch real-time traffic in the chart and log
4. Use filters to focus on specific processes/connections
5. Click "Export to CSV" to save the log
6. Click "Stop Monitoring" when done

## Keyboard Shortcuts
- **Ctrl+R**: Refresh process list
- **Delete**: Kill selected process
- **F5**: Refresh
- **Alt+F4**: Exit

## Known Limitations

1. **Network Monitoring**:
   - Requires Administrator privileges for ETW access
   - Does not capture actual packet payloads (only metadata)
   - Reverse DNS lookups may be slow for some IPs
   - GeoIP country detection not implemented

2. **Process Information**:
   - Some system processes cannot be accessed without admin
   - Command line may be unavailable for protected processes
   - Parent PID requires WMI query (may be slow)

3. **UI**:
   - Tree view is a simple toggle, not a true hierarchical view
   - Traffic chart is basic (placeholder for advanced visualization)
   - No graphing library integrated yet

4. **Cross-Platform**:
   - Windows-only (uses Windows-specific APIs)
   - Can be built on Linux/macOS but only runs on Windows

## Future Enhancements

- [ ] Npcap integration for packet-level capture with hex/ASCII view
- [ ] Advanced traffic charts with historical data
- [ ] Process creation/termination event monitoring
- [ ] Network connection blocking/firewall rules
- [ ] Export process list to various formats
- [ ] Dark/Light theme switching
- [ ] Persian language UI toggle
- [ ] Process grouping and favorites
- [ ] Performance profiles and alerts

## Security Considerations

- The application can kill processes and monitor network traffic
- Use with caution, especially when running as Administrator
- Critical system processes are flagged and require confirmation
- No telemetry or external network connections (except user-initiated DNS lookups)
- Open source - audit the code yourself

## License

This project is provided as-is for educational and utility purposes.

## Credits

Built with:
- [.NET](https://dotnet.microsoft.com/)
- [TraceEvent](https://github.com/microsoft/perfview) by Microsoft

---

**Note**: This is a powerful system utility. Always run with appropriate caution, especially when modifying processes or monitoring network traffic.
