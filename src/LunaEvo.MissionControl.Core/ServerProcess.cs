// LUNA EVO · LunaEvo.MissionControl.Core/ServerProcess.cs
// [Core] Finds a running Server.exe from a given folder (to warn before saving, or to follow a server started by hand).
// Used by: ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Diagnostics;

namespace LunaEvo.MissionControl.Core;

public static class ServerProcess
{
    /// <summary>True if a Server.exe from this folder is running. LMP only reads (and rewrites) its config at startup.</summary>
    public static bool IsRunning(string serverDirectory, string executableName = "Server.exe")
    {
        using var process = Find(serverDirectory, executableName);
        return process != null;
    }

    /// <summary>The running Server.exe from this folder, if any. The caller disposes it.</summary>
    public static Process? Find(string serverDirectory, string executableName = "Server.exe")
    {
        var expected = Path.GetFullPath(Path.Combine(serverDirectory, executableName));
        Process? found = null;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executableName)))
        {
            try
            {
                if (found == null && string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase))
                {
                    found = process;
                    continue;
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Can't inspect it (another user, or it just exited): assume it isn't ours.
            }
            process.Dispose();
        }
        return found;
    }
}
