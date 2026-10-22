// LUNA EVO · LunaEvo.MissionControl.Core/Ops/ServerPluginInstaller.cs
// [Core] Puts the LUNA EVO server plugin (and the Harmony it uses) into an LMP server's Plugins folder, updates or removes it.
// Used by: MapPage
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Security.Cryptography;

namespace LunaEvo.MissionControl.Core.Ops;

public enum PluginStatus { NotInstalled, Installed, Different }

/// <summary>
/// The plugin's DLLs travel inside Mission Control (no download), so installing is a copy. LMP loads every DLL under
/// Plugins/ at start and keeps it open, so changes need the server stopped and take effect on its next start. Since 2.7 the
/// plugin brings Harmony (for its P1 fix), so both live in Plugins/LunaEvo/; 2.4-2.6 put the plugin alone in Plugins/.
/// </summary>
public static class ServerPluginInstaller
{
    public const string FileName = "LunaEvo.ServerPlugin.dll";
    public const string HarmonyFileName = "0Harmony.dll";

    public static string PathFor(string serverDirectory) => Path.Combine(serverDirectory, "Plugins", "LunaEvo", FileName);
    public static string HarmonyPathFor(string serverDirectory) => Path.Combine(serverDirectory, "Plugins", "LunaEvo", HarmonyFileName);

    /// <summary>Where 2.4-2.6 installed it (the plugin alone, no Harmony).</summary>
    public static string LegacyPathFor(string serverDirectory) => Path.Combine(serverDirectory, "Plugins", FileName);

    /// <summary>Built against LMP 0.29.2's server (.NET 6); other versions may have changed what it uses.</summary>
    public static bool Supports(Version? lmp) => lmp is { Major: 0, Minor: 29 };

    /// <summary>Whether it's there, and whether it's this copy of Mission Control's plugin (same bytes, Harmony beside it).</summary>
    public static PluginStatus Status(string serverDirectory, byte[] plugin)
    {
        try
        {
            var path = PathFor(serverDirectory);
            if (!File.Exists(path))
                return File.Exists(LegacyPathFor(serverDirectory)) ? PluginStatus.Different : PluginStatus.NotInstalled;
            return File.Exists(HarmonyPathFor(serverDirectory)) && !File.Exists(LegacyPathFor(serverDirectory)) &&
                   SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(SHA256.HashData(plugin))
                ? PluginStatus.Installed
                : PluginStatus.Different;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return PluginStatus.Different; // can't read it: treat it as not ours
        }
    }

    /// <summary>Copies the plugin and Harmony in (each via a temp file, so LMP never loads half a DLL) and removes a 2.4-2.6 copy.</summary>
    public static void Install(string serverDirectory, byte[] plugin, byte[] harmony)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor(serverDirectory))!);
        Write(HarmonyPathFor(serverDirectory), harmony);
        Write(PathFor(serverDirectory), plugin);
        var legacy = LegacyPathFor(serverDirectory);
        if (File.Exists(legacy))
            File.Delete(legacy); // two copies would both load
    }

    public static void Remove(string serverDirectory)
    {
        foreach (var file in new[] { PathFor(serverDirectory), HarmonyPathFor(serverDirectory), LegacyPathFor(serverDirectory) })
            if (File.Exists(file))
                File.Delete(file);
        var folder = Path.GetDirectoryName(PathFor(serverDirectory))!;
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
        // Its last file would otherwise look like a stopped feed forever; LiveFeed ignores stale files anyway.
        var live = Live.LiveFeed.PathFor(serverDirectory);
        if (File.Exists(live))
            File.Delete(live);
    }

    private static void Write(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }
}
