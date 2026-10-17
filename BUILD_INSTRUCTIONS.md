# Build Instructions

This document provides step-by-step instructions for building the Process & Network Monitor application.

## Prerequisites

### Required
- **.NET 8 SDK** - Download from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

### System Requirements
- **Build Platform**: Windows, Linux, or macOS (cross-compilation supported)
- **Target Platform**: Windows 10/11 x64 only

## Quick Build

### On Windows

```powershell
# Clone the repository
git clone <repository-url>
cd ProcessNetMonitor

# Restore dependencies
dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj

# Build
dotnet build ProcessNetMonitor/ProcessNetMonitor.csproj -c Release

# Publish single-file executable
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj -c Release -r win-x64 --self-contained

# The executable will be at:
# ProcessNetMonitor\bin\Release\net8.0-windows\win-x64\publish\ProcessNetMonitor.exe
```

### On Linux / macOS

```bash
# Clone the repository
git clone <repository-url>
cd ProcessNetMonitor

# Restore dependencies
dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj

# Build (cross-compile for Windows)
dotnet build ProcessNetMonitor/ProcessNetMonitor.csproj -c Release

# Publish single-file executable
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj -c Release -r win-x64 --self-contained

# The executable will be at:
# ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe
```

## Build Configurations

### Debug Build
```bash
dotnet build ProcessNetMonitor/ProcessNetMonitor.csproj -c Debug
```

### Release Build (Optimized)
```bash
dotnet build ProcessNetMonitor/ProcessNetMonitor.csproj -c Release
```

### Self-Contained Single File (Recommended)
This creates a single portable .exe file with no external dependencies:

```bash
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
    -c Release \
    -r win-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true
```

### Framework-Dependent Build (Smaller, requires .NET Runtime)
```bash
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
    -c Release \
    -r win-x64 \
    --no-self-contained
```

## Running Tests

```bash
# Run all tests
dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj

# Run with detailed output
dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj -v detailed

# Run with code coverage (requires coverlet)
dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj /p:CollectCoverage=true
```

## Troubleshooting

### Issue: "SDK 'Microsoft.NET.Sdk.WindowsDesktop' not found"
**Solution**: Make sure you have .NET 8 SDK installed, not just the runtime.

```bash
dotnet --version  # Should show 8.0.x
```

### Issue: Build fails on Linux/macOS with "Windows-only" errors
**Solution**: The project uses `EnableWindowsTargeting=true` which should handle this automatically. Make sure you're using .NET 8 SDK.

### Issue: Published exe is very large (160MB+)
**Explanation**: This is expected for a self-contained, single-file deployment. It includes:
- .NET 8 runtime (~70MB)
- WPF libraries (~40MB)
- TraceEvent library (~20MB)
- Application code and dependencies (~30MB)

To reduce size, use framework-dependent deployment (requires .NET 8 Runtime on target):
```bash
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj -c Release -r win-x64
```
This will be ~5MB but requires .NET 8 Runtime installed on the target Windows machine.

### Issue: "Cannot find 'System.Management'"
**Solution**: Restore NuGet packages:
```bash
dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj
```

## Advanced Build Options

### Enable ReadyToRun (Faster startup)
```bash
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
    -c Release \
    -r win-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true
```

### Trim Unused Code (Smaller size, but risky)
```bash
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
    -c Release \
    -r win-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true
```
⚠️ **Warning**: Trimming may break reflection-based features. Test thoroughly.

## Creating a Release Package

```bash
# Build the release
dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
    -c Release \
    -r win-x64 \
    --self-contained \
    -p:PublishSingleFile=true

# Create artifacts folder
mkdir -p artifacts
cp ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe artifacts/
cp README.md artifacts/

# Create zip package
cd artifacts
zip -9 ProcessNetMonitor-v1.0-win-x64.zip ProcessNetMonitor.exe README.md
```

## Build Output Locations

| Build Type | Output Path |
|------------|-------------|
| Debug Build | `ProcessNetMonitor/bin/Debug/net8.0-windows/win-x64/` |
| Release Build | `ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/` |
| Published (Framework-Dependent) | `ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/` |
| Published (Self-Contained) | `ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe` |

## Continuous Integration

Example GitHub Actions workflow:

```yaml
name: Build

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore
      run: dotnet restore ProcessNetMonitor/ProcessNetMonitor.csproj
    
    - name: Build
      run: dotnet build ProcessNetMonitor/ProcessNetMonitor.csproj -c Release
    
    - name: Test
      run: dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj
    
    - name: Publish
      run: |
        dotnet publish ProcessNetMonitor/ProcessNetMonitor.csproj \
          -c Release -r win-x64 --self-contained \
          -p:PublishSingleFile=true
    
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: ProcessNetMonitor-win-x64
        path: ProcessNetMonitor/bin/Release/net8.0-windows/win-x64/publish/ProcessNetMonitor.exe
```

## Project Structure

```
ProcessNetMonitor/
├── ProcessNetMonitor/           # Main application
│   ├── Models/                  # Data models
│   ├── ViewModels/              # MVVM view models
│   ├── Services/                # Business logic
│   ├── Native/                  # P/Invoke declarations
│   ├── Styles/                  # UI themes
│   ├── App.xaml                 # Application entry
│   ├── MainWindow.xaml          # Main UI
│   └── ProcessNetMonitor.csproj # Project file
├── ProcessNetMonitor.Tests/     # Unit tests
│   ├── FilterParsingTests.cs
│   └── ProcessNetMonitor.Tests.csproj
└── README.md                    # Documentation
```

## Dependencies

The project uses these NuGet packages:
- **Microsoft.Diagnostics.Tracing.TraceEvent** (3.1.8) - ETW network monitoring
- **System.Management** (8.0.0) - WMI queries for process information

All dependencies are automatically restored during build.

## Support

For issues, questions, or contributions, please refer to the main README.md file.
