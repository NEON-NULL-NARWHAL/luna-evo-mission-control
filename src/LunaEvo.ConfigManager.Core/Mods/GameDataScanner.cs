// LUNA EVO · LunaEvo.ConfigManager.Core/Mods/GameDataScanner.cs
// [Core] Scans a KSP GameData folder for parts, resources, plugin DLLs and expansions, the way LMP's client does.
// Used by: ModImport (Mod Control page)
// Guide:   Meat and Potatoes/12-Mod-Control-From-GameData.md

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LunaEvo.ConfigManager.Core.Mods;

public sealed record PluginFile(string FilePath, string Sha, string Text);

public sealed record GameDataScan(
    IReadOnlyList<string> Parts,
    IReadOnlyList<string> Resources,
    IReadOnlyList<PluginFile> Plugins,
    IReadOnlyList<string> Expansions,
    int ConfigFilesRead,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Mirrors LMP 0.29.2's own generator (LmpClient/Systems/Mod/ModSystem.cs, GenerateModControlFile):
/// <list type="bullet">
/// <item>Parts: every PART node's name, with "_" turned into "." the way KSP's PartLoader does (delta_small → delta.small).</item>
/// <item>Resources: every RESOURCE_DEFINITION node's name.</item>
/// <item>Plugins: every .dll under GameData except Squad* and LunaMultiplayer, as a lowercase path relative to GameData
///   with "/" separators, SHA-256 as "AB-CD-...", and "Name. Version: x" text.</item>
/// <item>Expansions: folders under GameData/SquadExpansion (MakingHistory, Serenity).</item>
/// </list>
/// Limitation: ModuleManager patches (@PART, +PART, !PART) aren't applied, so parts created or removed by patches
/// are missed. LMP's in-game generator sees the patched result; use it if this matters.
/// </summary>
public static partial class GameDataScanner
{
    /// <summary>Recurse, but don't follow symlinks/junctions (a loop would never end) and skip unreadable folders.</summary>
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static GameDataScan Scan(string gameDataDirectory, bool hashPlugins = true)
    {
        if (!Directory.Exists(gameDataDirectory))
            throw new DirectoryNotFoundException($"{gameDataDirectory} doesn't exist.");

        var parts = new SortedSet<string>(StringComparer.Ordinal);
        var resources = new SortedSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var configs = 0;

        foreach (var cfg in Directory.EnumerateFiles(gameDataDirectory, "*.cfg", Recursive))
        {
            configs++;
            IReadOnlyList<KspNode> nodes;
            try
            {
                nodes = KspConfigParser.Parse(File.ReadAllText(cfg));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Couldn't read {Path.GetRelativePath(gameDataDirectory, cfg)}: {e.Message}");
                continue;
            }
            foreach (var node in nodes)
            {
                // "PART:NEEDS[SomeMod]" is a part that only exists when that mod is installed (ModuleManager syntax).
                if ((node.Name == "PART" || node.Name.StartsWith("PART:", StringComparison.Ordinal)) && node.Get("name") is { Length: > 0 } part)
                    parts.Add(part.Replace('_', '.'));
                else if (node.Name == "RESOURCE_DEFINITION" && node.Get("name") is { Length: > 0 } resource)
                    resources.Add(resource);
            }
        }

        var plugins = new List<PluginFile>();
        foreach (var modDirectory in Directory.GetDirectories(gameDataDirectory))
        {
            var folder = Path.GetFileName(modDirectory);
            if (folder.StartsWith("squad", StringComparison.OrdinalIgnoreCase) || folder.StartsWith("lunamultiplayer", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var dll in Directory.EnumerateFiles(modDirectory, "*.dll", Recursive).Order(StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(gameDataDirectory, dll).Replace('\\', '/').ToLowerInvariant();
                var version = FileVersionInfo.GetVersionInfo(dll).FileVersion;
                plugins.Add(new PluginFile(relative, hashPlugins ? Sha256(dll) : "", $"{Path.GetFileNameWithoutExtension(dll)}. Version: {version}"));
            }
        }

        var expansionsDir = Path.Combine(gameDataDirectory, "SquadExpansion");
        var expansions = Directory.Exists(expansionsDir)
            ? Directory.GetDirectories(expansionsDir).Select(Path.GetFileName).OfType<string>().Order().ToList()
            : [];

        return new GameDataScan(parts.ToList(), resources.ToList(), plugins, expansions, configs, warnings);
    }

    /// <summary>LMP's hash format: BitConverter.ToString(SHA256(file)), e.g. "3F-A0-...".</summary>
    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path); // streamed: mod DLLs can be big
        return BitConverter.ToString(SHA256.HashData(stream));
    }

    /// <summary>Looks for KSP's GameData in the Steam libraries on this PC. Null if not found.</summary>
    public static string? FindKspGameData()
    {
        var steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        var libraries = new List<string> { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            libraries.AddRange(LibraryPathRegex().Matches(File.ReadAllText(vdf)).Select(m => m.Groups[1].Value.Replace(@"\\", @"\")));

        return libraries
            .Select(lib => Path.Combine(lib, "steamapps", "common", "Kerbal Space Program", "GameData"))
            .FirstOrDefault(Directory.Exists);
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPathRegex();
}
