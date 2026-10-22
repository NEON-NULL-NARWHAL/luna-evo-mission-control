// LUNA EVO · LunaEvo.MissionControl.Core/Join/ClientPatches.cs
// [Core] Puts LUNA EVO's patches for the LMP client (a KSP mod) into a KSP install, updates them, or takes them out.
// Used by: JoinPage, KspLauncher (auto-connect to any port and with a password once they're in)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System.Security.Cryptography;
using LunaEvo.MissionControl.Core.Ops;

namespace LunaEvo.MissionControl.Core.Join;

/// <summary>
/// The patches are one DLL in GameData/000_LunaEvoPatches (the "000_" makes KSP load it before LMP). They travel inside
/// Mission Control, so installing is a copy. KSP must be closed: it keeps its DLLs open and LMP's settings in memory.
/// </summary>
public static class ClientPatches
{
    public const string FolderName = "000_LunaEvoPatches";
    public const string FileName = "LunaEvo.ClientPatches.dll";

    public static string PathFor(string kspFolder) => Path.Combine(kspFolder, "GameData", FolderName, FileName);

    /// <summary>Written and tested for the LMP 0.29 client; on others the patch turns itself off, so it isn't offered.</summary>
    public static bool Supports(Version? lmpClient) => lmpClient is { Major: 0, Minor: 29 };

    public static PluginStatus Status(string kspFolder, byte[] patches)
    {
        var path = PathFor(kspFolder);
        try
        {
            if (!File.Exists(path))
                return PluginStatus.NotInstalled;
            return SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(SHA256.HashData(patches)) ? PluginStatus.Installed : PluginStatus.Different;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return PluginStatus.Different;
        }
    }

    /// <summary>True if this KSP has the patches (any version of them): LMP there can take a port and a saved password.</summary>
    public static bool IsInstalled(string kspFolder) => File.Exists(PathFor(kspFolder));

    public static void Install(string kspFolder, byte[] patches)
    {
        if (KspLauncher.IsRunning(kspFolder))
            throw new InvalidOperationException("Close KSP first: it keeps its mods open while it runs.");
        var path = PathFor(kspFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, patches);
        File.Move(temp, path, overwrite: true);
    }

    public static void Remove(string kspFolder)
    {
        if (KspLauncher.IsRunning(kspFolder))
            throw new InvalidOperationException("Close KSP first: it keeps its mods open while it runs.");
        var folder = Path.GetDirectoryName(PathFor(kspFolder))!;
        File.Delete(PathFor(kspFolder));
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
    }
}
