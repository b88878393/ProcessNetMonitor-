# Process & Network Monitor

Portable Windows desktop app (.NET 8 / WPF) that replaces Task Manager’s weak process tools and adds a Wireshark-like per-application network view via ETW.

## Features (v1)

### Tab 1 — Processes
- Live list: name, PID, parent PID, user, CPU %, memory, threads, handles, start time, path, command line, company
- TCP/UDP ports per process (IPv4+IPv6 via `GetExtendedTcpTable` / `GetExtendedUdpTable`)
- Filter: name, path, command line; `pid:1234`; `:8080` / `port:8080`
- Kill, kill tree, kill all same name, suspend/resume, set priority, open location, copy details
- Critical-process warnings (System, csrss, wininit, lsass, …); restart-as-admin
- Sortable columns, parent/child tree ordering toggle, configurable auto-refresh

### Tab 2 — Network traffic
- Live per-process send/receive via ETW (kernel NetworkTCPIP) — **admin required**, no driver
- Totals + per-process upload/download rates
- Connections/events log with filter, pause, CSV export
- Reverse DNS (async, cached)
- Npcap detection (status only in v1; app works fully without Npcap — no packet payloads yet)

## Requirements
- Windows 10/11 x64
- Admin for network ETW tab
- Optional: Npcap (future packet view; not required)

## Build (Linux or Windows)

```bash
dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj
dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj -c Release -r win-x64 --self-contained
```

Output: `ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe`

Uses `EnableWindowsTargeting`, `PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`.

## Run
Copy the single-file exe anywhere and run. Use **File → Restart as Administrator** for the Network tab.

## Project layout
- `ProcessNetMonitor/` — WPF app (MVVM)
- `ProcessNetMonitor.Core/` — pure logic (filters, formatting) for Linux unit tests
- `ProcessNetMonitor.Tests/` — xUnit tests

## Known limitations
- Packet payload / SharpPcap hex view not implemented (Npcap is detected only)
- Traffic “chart” is a live rate list, not a time-series graph library
- Some protected processes omit path/command line without admin
- Windows-only runtime (cross-build from Linux is supported)
