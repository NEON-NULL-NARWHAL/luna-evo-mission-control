// LUNA EVO · LunaEvo.ConfigManager.Core/Server/ProcessStats.cs
// [Core] Server health: memory and CPU use of Server.exe, sampled every couple of seconds.
// Used by: ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Diagnostics;

namespace LunaEvo.ConfigManager.Core.Server;

/// <param name="CpuPercent">Share of the whole PC's CPU (all cores = 100%) since the previous sample.</param>
public sealed record ServerHealth(long MemoryBytes, double CpuPercent, int Threads);

/// <summary>Samples one process. CPU needs two samples, so the first one reports 0%.</summary>
public sealed class ProcessStats
{
    private int? _pid;
    private TimeSpan _lastCpu;
    private DateTime _lastWall;

    /// <summary>The process's health now, or null if it isn't running (or can't be read).</summary>
    public ServerHealth? Sample(int? pid, DateTime? now = null)
    {
        if (pid is not { } id)
        {
            _pid = null;
            return null;
        }
        try
        {
            using var process = Process.GetProcessById(id);
            process.Refresh();
            var wall = now ?? DateTime.UtcNow;
            var cpu = process.TotalProcessorTime;
            var percent = 0.0;
            if (_pid == id && wall > _lastWall)
                percent = Math.Clamp((cpu - _lastCpu).TotalMilliseconds / (wall - _lastWall).TotalMilliseconds / Environment.ProcessorCount * 100, 0, 100);
            (_pid, _lastCpu, _lastWall) = (id, cpu, wall);
            return new ServerHealth(process.WorkingSet64, percent, process.Threads.Count);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _pid = null; // exited, or not ours to read
            return null;
        }
    }
}
