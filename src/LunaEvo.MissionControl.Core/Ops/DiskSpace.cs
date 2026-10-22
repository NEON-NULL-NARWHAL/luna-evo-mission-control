// LUNA EVO · LunaEvo.MissionControl.Core/Ops/DiskSpace.cs
// [Core] Warns when the drive the server runs from is nearly full (LMP can lose save files when it fills).
// Used by: ServerPage (banner and an alert)
// Guide:   Meat and Potatoes/13-Automation.md

namespace LunaEvo.MissionControl.Core.Ops;

/// <summary>
/// LMP writes its universe files in place. When the drive filled up during its backup (the owner's server,
/// 2026-10-22), it crashed mid-save and left a scenario file empty. Nothing said the drive was full until then.
/// </summary>
public static class DiskSpace
{
    /// <summary>Below this, a warning: room for a few backups of a big universe, not much more.</summary>
    public const long LowBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>Free bytes on the drive of a folder, or null if it can't tell. Tests replace it.</summary>
    public static Func<string, long?> FreeBytes { get; set; } = folder =>
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    };

    /// <summary>A warning for the server's drive if it's nearly full, else null.</summary>
    public static string? Warning(string serverDirectory)
    {
        if (FreeBytes(serverDirectory) is not { } free || free >= LowBytes)
            return null;
        var drive = Path.GetPathRoot(Path.GetFullPath(serverDirectory))?.TrimEnd('\\', '/') ?? "the drive";
        return $"Only {free / (1024.0 * 1024 * 1024):0.0} GB free on {drive}, where this server is. If the drive fills up, LMP can " +
               "lose save files while it writes them. Free up some space (or move the server to a drive with more).";
    }
}
