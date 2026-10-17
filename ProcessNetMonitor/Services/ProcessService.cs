using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ProcessNetMonitor.Models;
using ProcessNetMonitor.Native;

namespace ProcessNetMonitor.Services;

public class ProcessService
{
    private readonly Dictionary<int, (DateTime lastCheck, TimeSpan lastCpu)> _cpuUsageCache = new();
    private static readonly string[] CriticalProcessNames = 
    { 
        "system", "csrss", "wininit", "services", "lsass", "winlogon", "smss" 
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
        var portToPidMap = GetPortToPidMap();
        
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var processInfo = new ProcessInfo
                {
                    Name = process.ProcessName,
                    ProcessId = process.Id,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    StartTime = process.StartTime,
                    IsCriticalSystem = IsCriticalProcess(process.ProcessName)
                };
                
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
                
                try
                {
                    processInfo.ParentProcessId = GetParentProcessId(process.Id);
                }
                catch { }
                
                try
                {
                    processInfo.UserName = GetProcessUser(process);
                }
                catch { }
                
                try
                {
                    processInfo.CpuPercent = GetCpuUsage(process);
                }
                catch { }
                
                try
                {
                    processInfo.PriorityClass = process.PriorityClass.ToString();
                }
                catch { }
                
                var pidPorts = portToPidMap.Where(kvp => kvp.Value == process.Id).Select(kvp => kvp.Key).ToList();
                foreach (var (port, protocol) in pidPorts)
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
                // Skip processes we can't access
            }
            finally
            {
                process.Dispose();
            }
        }
        
        return processes;
    }
    
    private Dictionary<(int port, string protocol), int> GetPortToPidMap()
    {
        var map = new Dictionary<(int, string), int>();
        
        // Get TCP IPv4 connections
        GetTcpConnections(NativeMethods.AF_INET, map);
        
        // Get TCP IPv6 connections
        GetTcpConnections(NativeMethods.AF_INET6, map);
        
        // Get UDP IPv4 connections
        GetUdpConnections(NativeMethods.AF_INET, map);
        
        // Get UDP IPv6 connections
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
                var table = Marshal.PtrToStructure<NativeMethods.MIB_TCPTABLE_OWNER_PID>(tcpTablePtr);
                IntPtr rowPtr = tcpTablePtr + Marshal.SizeOf<uint>();
                
                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                    int port = (int)((row.localPort >> 8) | ((row.localPort & 0xFF) << 8));
                    map[(port, "TCP")] = row.owningPid;
                    rowPtr += Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();
                }
            }
            else
            {
                var table = Marshal.PtrToStructure<NativeMethods.MIB_TCP6TABLE_OWNER_PID>(tcpTablePtr);
                IntPtr rowPtr = tcpTablePtr + Marshal.SizeOf<uint>();
                
                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCP6ROW_OWNER_PID>(rowPtr);
                    int port = (int)((row.localPort >> 8) | ((row.localPort & 0xFF) << 8));
                    map[(port, "TCP")] = row.owningPid;
                    rowPtr += Marshal.SizeOf<NativeMethods.MIB_TCP6ROW_OWNER_PID>();
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
                var table = Marshal.PtrToStructure<NativeMethods.MIB_UDPTABLE_OWNER_PID>(udpTablePtr);
                IntPtr rowPtr = udpTablePtr + Marshal.SizeOf<uint>();
                
                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);
                    int port = (int)((row.localPort >> 8) | ((row.localPort & 0xFF) << 8));
                    map[(port, "UDP")] = row.owningPid;
                    rowPtr += Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();
                }
            }
            else
            {
                var table = Marshal.PtrToStructure<NativeMethods.MIB_UDP6TABLE_OWNER_PID>(udpTablePtr);
                IntPtr rowPtr = udpTablePtr + Marshal.SizeOf<uint>();
                
                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDP6ROW_OWNER_PID>(rowPtr);
                    int port = (int)((row.localPort >> 8) | ((row.localPort & 0xFF) << 8));
                    map[(port, "UDP")] = row.owningPid;
                    rowPtr += Marshal.SizeOf<NativeMethods.MIB_UDP6ROW_OWNER_PID>();
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(udpTablePtr);
        }
    }
    
    private double GetCpuUsage(Process process)
    {
        try
        {
            var now = DateTime.Now;
            var currentCpu = process.TotalProcessorTime;
            
            if (_cpuUsageCache.TryGetValue(process.Id, out var cached))
            {
                var timeDiff = (now - cached.lastCheck).TotalMilliseconds;
                var cpuDiff = (currentCpu - cached.lastCpu).TotalMilliseconds;
                
                if (timeDiff > 0)
                {
                    var cpuPercent = (cpuDiff / timeDiff / Environment.ProcessorCount) * 100;
                    _cpuUsageCache[process.Id] = (now, currentCpu);
                    return Math.Round(cpuPercent, 2);
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
    
    private int GetParentProcessId(int processId)
    {
        try
        {
            var query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}";
            using var searcher = new System.Management.ManagementObjectSearcher(query);
            using var results = searcher.Get();
            
            foreach (var obj in results)
            {
                return Convert.ToInt32(obj["ParentProcessId"]);
            }
        }
        catch { }
        
        return 0;
    }
    
    private string GetProcessUser(Process process)
    {
        try
        {
            var query = $"SELECT * FROM Win32_Process WHERE ProcessId = {process.Id}";
            using var searcher = new System.Management.ManagementObjectSearcher(query);
            using var results = searcher.Get();
            
            foreach (System.Management.ManagementObject obj in results)
            {
                var args = new object?[] { null, null };
                obj.InvokeMethod("GetOwner", args);
                if (args[0] != null)
                {
                    return args[1] + "\\" + args[0];
                }
            }
        }
        catch { }
        
        return string.Empty;
    }
    
    private bool IsCriticalProcess(string processName)
    {
        return CriticalProcessNames.Contains(processName.ToLower());
    }
    
    public bool KillProcess(int processId, bool force = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (IsCriticalProcess(process.ProcessName) && !force)
            {
                return false;
            }
            
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public bool KillProcessTree(int processId)
    {
        var children = GetChildProcesses(processId);
        foreach (var childPid in children)
        {
            KillProcessTree(childPid);
        }
        
        return KillProcess(processId);
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
            {
                children.Add(Convert.ToInt32(obj["ProcessId"]));
            }
        }
        catch { }
        
        return children;
    }
    
    public bool SuspendProcess(int processId)
    {
        try
        {
            var handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_SUSPEND_RESUME, false, processId);
            
            if (handle == IntPtr.Zero)
                return false;
            
            try
            {
                return NativeMethods.NtSuspendProcess(handle) == 0;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }
        catch
        {
            return false;
        }
    }
    
    public bool ResumeProcess(int processId)
    {
        try
        {
            var handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_SUSPEND_RESUME, false, processId);
            
            if (handle == IntPtr.Zero)
                return false;
            
            try
            {
                return NativeMethods.NtResumeProcess(handle) == 0;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }
        catch
        {
            return false;
        }
    }
    
    public bool SetProcessPriority(int processId, ProcessPriorityClass priority)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.PriorityClass = priority;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
