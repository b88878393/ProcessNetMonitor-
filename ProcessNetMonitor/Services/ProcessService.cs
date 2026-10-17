using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ProcessNetMonitor.Models;
using ProcessNetMonitor.Native;

namespace ProcessNetMonitor.Services;

public class ProcessService
{
    private readonly Dictionary<int, (DateTime lastCheck, TimeSpan lastCpu)> _cpuUsageCache = new();
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "csrss", "wininit", "services", "lsass", "winlogon", "smss"
    };

    public static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public List<ProcessInfo> GetAllProcesses()
    {
        var processes = new List<ProcessInfo>();
        var wmi = GetWmiProcessMap();
        var portToPidMap = GetPortToPidMap();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processInfo = new ProcessInfo
                {
                    Name = process.ProcessName,
                    ProcessId = process.Id,
                    ThreadCount = Safe(() => process.Threads.Count, 0),
                    HandleCount = Safe(() => process.HandleCount, 0),
                    IsCriticalSystem = CriticalProcessNames.Contains(process.ProcessName)
                };

                try { processInfo.StartTime = process.StartTime; } catch { }

                try
                {
                    processInfo.WorkingSet = process.WorkingSet64;
                    processInfo.PrivateBytes = process.PrivateMemorySize64;
                    processInfo.MemoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
                }
                catch { }

                try
                {
                    processInfo.ExecutablePath = process.MainModule?.FileName ?? string.Empty;
                    processInfo.Company = process.MainModule?.FileVersionInfo.CompanyName ?? string.Empty;
                }
                catch { }

                try { processInfo.CpuPercent = GetCpuUsage(process); } catch { }
                try { processInfo.PriorityClass = process.PriorityClass.ToString(); } catch { }

                if (wmi.TryGetValue(process.Id, out var meta))
                {
                    processInfo.ParentProcessId = meta.ParentPid;
                    if (!string.IsNullOrEmpty(meta.CommandLine))
                        processInfo.CommandLine = meta.CommandLine;
                    if (string.IsNullOrEmpty(processInfo.ExecutablePath) && !string.IsNullOrEmpty(meta.ExecutablePath))
                        processInfo.ExecutablePath = meta.ExecutablePath;
                    processInfo.UserName = meta.UserName;
                }

                foreach (var (port, protocol) in portToPidMap.Where(kvp => kvp.Value == process.Id).Select(kvp => kvp.Key))
                {
                    if (protocol == "TCP")
                        processInfo.TcpPorts.Add(port);
                    else
                        processInfo.UdpPorts.Add(port);
                }

                processes.Add(processInfo);
            }
            catch
            {
                // Skip inaccessible processes
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes;
    }

    private static T Safe<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    private readonly record struct WmiMeta(int ParentPid, string CommandLine, string ExecutablePath, string UserName);

    /// <summary>One WMI pass for parent PID, command line, and owner (cheaper than per-process queries).</summary>
    private Dictionary<int, WmiMeta> GetWmiProcessMap()
    {
        var map = new Dictionary<int, WmiMeta>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, CommandLine, ExecutablePath FROM Win32_Process");
            using var results = searcher.Get();

            foreach (System.Management.ManagementObject obj in results)
            {
                try
                {
                    var pid = Convert.ToInt32(obj["ProcessId"]);
                    var parent = Convert.ToInt32(obj["ParentProcessId"] ?? 0);
                    var cmd = obj["CommandLine"]?.ToString() ?? string.Empty;
                    var path = obj["ExecutablePath"]?.ToString() ?? string.Empty;
                    var user = string.Empty;

                    try
                    {
                        var args = new object?[] { null, null };
                        obj.InvokeMethod("GetOwner", args);
                        if (args[0] != null)
                            user = $"{args[1]}\\{args[0]}";
                    }
                    catch { }

                    map[pid] = new WmiMeta(parent, cmd, path, user);
                }
                catch { }
            }
        }
        catch
        {
            // WMI unavailable — continue with partial data
        }

        return map;
    }

    private Dictionary<(int port, string protocol), int> GetPortToPidMap()
    {
        var map = new Dictionary<(int, string), int>();
        GetTcpConnections(NativeMethods.AF_INET, map);
        GetTcpConnections(NativeMethods.AF_INET6, map);
        GetUdpConnections(NativeMethods.AF_INET, map);
        GetUdpConnections(NativeMethods.AF_INET6, map);
        return map;
    }

    private void GetTcpConnections(int addressFamily, Dictionary<(int, string), int> map)
    {
        int bufferSize = 0;
        uint result = NativeMethods.GetExtendedTcpTable(
            IntPtr.Zero, ref bufferSize, true, addressFamily,
            NativeMethods.TcpTableClass.TCP_TABLE_OWNER_PID_ALL);

        if (result != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
            return;

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = NativeMethods.GetExtendedTcpTable(
                tcpTablePtr, ref bufferSize, true, addressFamily,
                NativeMethods.TcpTableClass.TCP_TABLE_OWNER_PID_ALL);

            if (result != 0)
                return;

            if (addressFamily == NativeMethods.AF_INET)
            {
                var numEntries = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = tcpTablePtr + Marshal.SizeOf<uint>();
                var rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                    int port = Ntohs(row.localPort);
                    map[(port, "TCP")] = row.owningPid;
                    rowPtr += rowSize;
                }
            }
            else
            {
                var numEntries = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = tcpTablePtr + Marshal.SizeOf<uint>();
                var rowSize = Marshal.SizeOf<NativeMethods.MIB_TCP6ROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCP6ROW_OWNER_PID>(rowPtr);
                    int port = Ntohs(row.localPort);
                    map[(port, "TCP")] = row.owningPid;
                    rowPtr += rowSize;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
    }

    private void GetUdpConnections(int addressFamily, Dictionary<(int, string), int> map)
    {
        int bufferSize = 0;
        uint result = NativeMethods.GetExtendedUdpTable(
            IntPtr.Zero, ref bufferSize, true, addressFamily,
            NativeMethods.UdpTableClass.UDP_TABLE_OWNER_PID);

        if (result != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
            return;

        IntPtr udpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = NativeMethods.GetExtendedUdpTable(
                udpTablePtr, ref bufferSize, true, addressFamily,
                NativeMethods.UdpTableClass.UDP_TABLE_OWNER_PID);

            if (result != 0)
                return;

            if (addressFamily == NativeMethods.AF_INET)
            {
                var numEntries = Marshal.ReadInt32(udpTablePtr);
                IntPtr rowPtr = udpTablePtr + Marshal.SizeOf<uint>();
                var rowSize = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);
                    int port = Ntohs(row.localPort);
                    map[(port, "UDP")] = row.owningPid;
                    rowPtr += rowSize;
                }
            }
            else
            {
                var numEntries = Marshal.ReadInt32(udpTablePtr);
                IntPtr rowPtr = udpTablePtr + Marshal.SizeOf<uint>();
                var rowSize = Marshal.SizeOf<NativeMethods.MIB_UDP6ROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDP6ROW_OWNER_PID>(rowPtr);
                    int port = Ntohs(row.localPort);
                    map[(port, "UDP")] = row.owningPid;
                    rowPtr += rowSize;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(udpTablePtr);
        }
    }

    private static int Ntohs(uint port) => (int)((port >> 8) | ((port & 0xFF) << 8));

    private double GetCpuUsage(Process process)
    {
        try
        {
            var now = DateTime.UtcNow;
            var currentCpu = process.TotalProcessorTime;

            if (_cpuUsageCache.TryGetValue(process.Id, out var cached))
            {
                var timeDiff = (now - cached.lastCheck).TotalMilliseconds;
                var cpuDiff = (currentCpu - cached.lastCpu).TotalMilliseconds;

                if (timeDiff > 0)
                {
                    var cpuPercent = (cpuDiff / timeDiff / Environment.ProcessorCount) * 100;
                    _cpuUsageCache[process.Id] = (now, currentCpu);
                    return Math.Round(Math.Clamp(cpuPercent, 0, 100 * Environment.ProcessorCount), 2);
                }
            }

            _cpuUsageCache[process.Id] = (now, currentCpu);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public bool KillProcess(int processId, bool force = false)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (CriticalProcessNames.Contains(process.ProcessName) && !force)
                return false;

            process.Kill(entireProcessTree: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool KillProcessTree(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (CriticalProcessNames.Contains(process.ProcessName))
                return false;

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            // Fallback: manual tree walk
            foreach (var childPid in GetChildProcesses(processId))
                KillProcessTree(childPid);
            return KillProcess(processId);
        }
    }

    private List<int> GetChildProcesses(int parentId)
    {
        var children = new List<int>();
        try
        {
            var query = $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentId}";
            using var searcher = new System.Management.ManagementObjectSearcher(query);
            using var results = searcher.Get();
            foreach (var obj in results)
                children.Add(Convert.ToInt32(obj["ProcessId"]));
        }
        catch { }

        return children;
    }

    public bool SuspendProcess(int processId)
    {
        try
        {
            var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_SUSPEND_RESUME, false, processId);
            if (handle == IntPtr.Zero)
                return false;
            try { return NativeMethods.NtSuspendProcess(handle) == 0; }
            finally { NativeMethods.CloseHandle(handle); }
        }
        catch { return false; }
    }

    public bool ResumeProcess(int processId)
    {
        try
        {
            var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_SUSPEND_RESUME, false, processId);
            if (handle == IntPtr.Zero)
                return false;
            try { return NativeMethods.NtResumeProcess(handle) == 0; }
            finally { NativeMethods.CloseHandle(handle); }
        }
        catch { return false; }
    }

    public bool SetProcessPriority(int processId, ProcessPriorityClass priority)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.PriorityClass = priority;
            return true;
        }
        catch { return false; }
    }
}
