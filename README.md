# ProcessNetMonitor

**Portable Windows process manager + network traffic monitor** — a fast Task Manager alternative with a Wireshark-like per-application network view.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D6)](#requirements)

Author: **[Mahyar Tolooie](https://tolooie.ir)**

---

## Features

### Processes
- Live list of running processes: name, PID, parent PID, user, CPU %, memory, threads, handles, start time, path, command line, company
- Fast search/filter by **name**, **PID** (`pid:1234`), **port** (`:8080` / `port:8080`), **path**, and **command line**
- Per-process **TCP/UDP** connections (IPv4 + IPv6) via `GetExtendedTcpTable` / `GetExtendedUdpTable`
- **Kill**, **kill process tree**, kill all with the same name, **suspend/resume**, set priority
- Open file location, copy details, critical-system-process protection, restart as Administrator
- Sortable columns, parent/child tree ordering, configurable auto-refresh

### Network traffic
- Live **per-process** send/receive via **ETW** (kernel NetworkTCPIP) — no driver required
- Per-app traffic rates and destinations (local/remote address, protocol, direction, size)
- Connections/events log with filter, pause/resume, and **CSV export**
- Async reverse DNS (cached)
- Optional **Npcap** detection (status shown; packet payload view is on the roadmap)

### Packaging
- Single-file, self-contained **win-x64** portable `.exe` — no installer

---

## Screenshots

> Screenshots coming soon. Contributions welcome — see [Contributing](#contributing).

<!--
| Processes | Network |
|-----------|---------|
| ![Processes](docs/screenshots/processes.png) | ![Network](docs/screenshots/network.png) |
-->

---

## Requirements

- **OS:** Windows 10 or Windows 11 (**x64**)
- **Privileges:** Normal user for the Processes tab; **Administrator** for the Network (ETW) tab
- **Optional:** [Npcap](https://npcap.com/) for future packet-payload capture (not required for current features)

---

## Download & run

1. Open the [**Releases**](https://github.com/b88878393/ProcessNetMonitor-/releases) page
2. Download the latest `ProcessNetMonitor` portable package
3. Extract and run `ProcessNetMonitor.exe` (no install)
4. For network monitoring: **File → Restart as Administrator**

---

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). You can cross-compile from Linux/macOS (`EnableWindowsTargeting`).

```bash
git clone https://github.com/b88878393/ProcessNetMonitor-.git
cd ProcessNetMonitor-

dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj
dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
  -c Release \
  -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output:

```text
ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe
```

---

## Project structure

```text
ProcessNetMonitor/          # WPF app (.NET 8, MVVM)
  Models/                   # ProcessInfo, NetworkEvent, …
  ViewModels/               # MainViewModel, commands
  Services/                 # ProcessService, NetworkMonitorService (ETW), NpcapDetector
  Native/                   # P/Invoke (iphlpapi, ntdll, …)
  Styles/                   # Dark theme
ProcessNetMonitor.Core/     # Pure logic (filters, formatting) — unit-tested on Linux
ProcessNetMonitor.Tests/    # xUnit tests
```

---

## Roadmap

- [ ] Packet payload / hex+ASCII view via **SharpPcap** when Npcap is installed
- [ ] Time-series throughput charts (history, not only live rates)
- [ ] Export to **pcap** and richer **CSV** reports
- [ ] Light theme and optional Persian UI labels
- [ ] True hierarchical process tree control

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Short version:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-change`)
3. Commit with a clear message
4. Open a Pull Request against `main`

Please keep PRs focused, match existing MVVM style, and add/adjust unit tests in `ProcessNetMonitor.Core` / `ProcessNetMonitor.Tests` when you change pure logic.

---

## License

This project is licensed under the [MIT License](LICENSE).

Copyright (c) 2026 [Mahyar Tolooie](https://tolooie.ir)

---

## Acknowledgments

- [.NET](https://dotnet.microsoft.com/) and WPF
- [Microsoft TraceEvent](https://github.com/microsoft/perfview) for ETW
