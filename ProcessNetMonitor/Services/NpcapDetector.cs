using System.IO;
using System.Runtime.InteropServices;

namespace ProcessNetMonitor.Services;

/// <summary>
/// Detects whether Npcap is installed. Packet payload capture is optional and not required for v1.
/// Does not bundle or load drivers — detection only.
/// </summary>
public static class NpcapDetector
{
    public static bool IsInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            // Common install locations for Npcap / WinPcap-compatible stack
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "Npcap.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Npcap", "npcap.sys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Npcap", "NPFInstall.exe"),
                @"C:\Windows\System32\Npcap\wpcap.dll",
                @"C:\Windows\System32\wpcap.dll"
            };

            return candidates.Any(File.Exists);
        }
        catch
        {
            return false;
        }
    }

    public static string StatusMessage =>
        IsInstalled()
            ? "Npcap detected — Packet payload view available in a future update (app works fully without it)."
            : "Npcap not installed — ETW monitoring works without packet payloads.";
}
