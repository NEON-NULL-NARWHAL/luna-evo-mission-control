// LUNA EVO · LunaEvo.MissionControl.Core/Ops/ServerPluginInstaller.cs
// [Core] Puts the LUNA EVO server plugin into an LMP server's Plugins folder, updates it, or takes it out.
// Used by: MapPage
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Security.Cryptography;

namespace LunaEvo.MissionControl.Core.Ops;

public enum PluginStatus { NotInstalled, Installed, Different }

/// <summary>
/// The plugin's DLL travels inside Mission Control (no download), so installing is a copy. LMP loads every DLL in
/// Plugins/ at start and keeps it open, so changes need the server stopped and take effect on its next start.
/// </summary>
public static class ServerPluginInstaller
{
    public const string FileName = "LunaEvo.ServerPlugin.dll";

    public static string PathFor(string serverDirectory) => Path.Combine(serverDirectory, "Plugins", FileName);

    /// <summary>Built against LMP 0.29.2's server (.NET 6); other versions may have changed what it uses.</summary>
    public static bool Supports(Version? lmp) => lmp is { Major: 0, Minor: 29 };

    /// <summary>Whether it's there, and whether it's this copy of Mission Control's plugin (same bytes).</summary>
    public static PluginStatus Status(string serverDirectory, byte[] plugin)
    {
        var path = PathFor(serverDirectory);
        try
        {
            if (!File.Exists(path))
                return PluginStatus.NotInstalled;
            return SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(SHA256.HashData(plugin)) ? PluginStatus.Installed : PluginStatus.Different;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return PluginStatus.Different; // can't read it: treat it as not ours
        }
    }

    /// <summary>Copies the plugin in (via a temp file, so a failed copy never leaves half a DLL for LMP to load).</summary>
    public static void Install(string serverDirectory, byte[] plugin)
    {
        var path = PathFor(serverDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, plugin);
        File.Move(temp, path, overwrite: true);
    }

    public static void Remove(string serverDirectory)
    {
        File.Delete(PathFor(serverDirectory));
        // Its last file would otherwise look like a stopped feed forever; LiveFeed ignores stale files anyway.
        var live = Live.LiveFeed.PathFor(serverDirectory);
        if (File.Exists(live))
            File.Delete(live);
    }
}
