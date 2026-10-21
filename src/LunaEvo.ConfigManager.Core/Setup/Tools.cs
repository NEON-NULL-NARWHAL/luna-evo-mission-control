// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/Tools.cs
// [Core] Player tools LUNA EVO manages: CKAN (downloaded, versioned, verified) and the LMP client mod in a KSP install.
// Used by: SettingsPage (Downloads tab), JoinPage (CKAN installs, CKAN's launch options)
// Guide:   Meat and Potatoes/22-CKAN-and-LMP-Client.md

using System.IO.Compression;
using System.Text.Json;

namespace LunaEvo.ConfigManager.Core.Setup;

/// <summary>A KSP install CKAN manages (from %LOCALAPPDATA%\CKAN\config.json).</summary>
public sealed record CkanInstance(string Name, string Path);

/// <summary>
/// CKAN, the KSP mod manager: LUNA EVO keeps its own verified copies of the official <c>ckan.exe</c> under
/// <c>%LOCALAPPDATA%\LunaEvo\Tools\CKAN\&lt;version&gt;\</c>, so there's one known copy per version instead of copies
/// scattered around Downloads. Also reads what an existing CKAN set up: its game instances and its launch options.
/// </summary>
public static class CkanTools
{
    public const string ReleasesUrl = "https://api.github.com/repos/KSP-CKAN/CKAN/releases?per_page=10";
    public const string ExeName = "ckan.exe";

    /// <summary>Where LUNA EVO keeps its CKAN copies. Tests point it at a temp folder.</summary>
    public static string Root { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunaEvo", "Tools", "CKAN");

    /// <summary>CKAN's own settings folder (instances). Tests point it at a temp folder.</summary>
    public static string ConfigFolder { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CKAN");

    /// <summary>CKAN releases that have a verifiable ckan.exe, newest first.</summary>
    public static async Task<IReadOnlyList<(LmpRelease Release, LmpAsset Exe)>> AvailableAsync(HttpClient http, bool includePrereleases = false, CancellationToken cancel = default) =>
        (await LmpReleaseFeed.GetAllAsync(http, ReleasesUrl, includePrereleases, cancel))
            .Select(r => (Release: r, Exe: r.Assets.FirstOrDefault(a => a.Name.Equals(ExeName, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Exe is { Sha256: not null })
            .Select(x => (x.Release, x.Exe!))
            .ToList();

    /// <summary>The CKAN versions LUNA EVO has downloaded: (version folder, ckan.exe path), newest first.</summary>
    public static IReadOnlyList<(string Version, string Exe)> Installed() =>
        Directory.Exists(Root)
            ? Directory.GetDirectories(Root)
                .Select(d => (Version: System.IO.Path.GetFileName(d)!, Exe: System.IO.Path.Combine(d, ExeName)))
                .Where(x => File.Exists(x.Exe))
                .OrderByDescending(x => LmpVersion.Parse(x.Version) ?? new Version(0, 0))
                .ToList()
            : [];

    /// <summary>Downloads a CKAN release's ckan.exe (GitHub only, SHA-256 checked) into its version folder. Returns the exe.</summary>
    public static async Task<string> DownloadAsync(HttpClient http, LmpRelease release, LmpAsset exe, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var downloaded = await new ServerInstaller(http).DownloadAsync(exe, progress, cancel, allowUnverified: false);
        var folder = Directory.CreateDirectory(System.IO.Path.Combine(Root, SafeVersion(release.Tag))).FullName;
        var target = System.IO.Path.Combine(folder, ExeName);
        File.Copy(downloaded, target, overwrite: true);
        if (ServerInstaller.Sha256Of(target) != exe.Sha256) // checked again where it will run from
        {
            File.Delete(target);
            throw new DownloadVerificationException("The CKAN download doesn't match GitHub's checksum. It was removed.");
        }
        return target;
    }

    /// <summary>Removes a downloaded CKAN version. CKAN's own data (instances, downloads cache) isn't touched.</summary>
    public static void Remove(string version)
    {
        var folder = System.IO.Path.Combine(Root, SafeVersion(version));
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    /// <summary>The KSP installs CKAN knows about, whichever CKAN copy set them up.</summary>
    public static IReadOnlyList<CkanInstance> Instances()
    {
        try
        {
            var path = System.IO.Path.Combine(ConfigFolder, "config.json");
            if (!File.Exists(path))
                return [];
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("GameInstances", out var list) || list.ValueKind != JsonValueKind.Array)
                return [];
            return list.EnumerateArray()
                .Where(i => i.ValueKind == JsonValueKind.Object)
                .Select(i => new CkanInstance(Str(i, "Name"), Str(i, "Path")))
                .Where(i => i.Path.Length > 0 && Directory.Exists(i.Path))
                .ToList();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// The options CKAN's Play button passes to KSP for this install (CKAN/GUIConfig.json "CommandLines", first entry,
    /// minus the exe), e.g. ["-single-instance"]. Empty if CKAN isn't set up there.
    /// </summary>
    public static IReadOnlyList<string> LaunchOptions(string kspFolder)
    {
        try
        {
            var path = System.IO.Path.Combine(kspFolder, "CKAN", "GUIConfig.json");
            if (!File.Exists(path))
                return [];
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("CommandLines", out var lines) || lines.ValueKind != JsonValueKind.Array ||
                lines.GetArrayLength() == 0 || lines[0].ValueKind != JsonValueKind.String)
                return [];
            var parts = lines[0].GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // The first word is the exe CKAN runs; only plain "-option" words after it are passed on.
            return parts.Skip(1).Where(p => p.StartsWith('-') && !p.Contains('"') && p.Length < 80).Take(20).ToList();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string SafeVersion(string tag) => new string(tag.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray()) is { Length: > 0 } s ? s : "unknown";

    private static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

/// <summary>
/// Installs the LMP client mod (LunaMultiplayer-Client-Release.zip: GameData/LunaMultiplayer and GameData/000_Harmony)
/// into a KSP install. Keeps the player's LMP settings (GameData/LunaMultiplayer/Data: server list, saved mod rules),
/// backs the old version up outside GameData (KSP would load a backup inside it), and never replaces an existing
/// 000_Harmony, which other mods (and CKAN) share.
/// </summary>
public static class LmpClientInstaller
{
    public const long MaxUnpackedBytes = 200L * 1024 * 1024;
    public const int MaxEntries = 5000;

    /// <summary>The client zip of an LMP release, if it has one.</summary>
    public static LmpAsset? ClientAsset(LmpRelease release) =>
        release.Assets.FirstOrDefault(a => a.Name.Equals("LunaMultiplayer-Client-Release.zip", StringComparison.OrdinalIgnoreCase));

    /// <summary>Installs the client. Returns the backup folder of the previous version, or null if there wasn't one.</summary>
    public static string? Install(string zipPath, string kspFolder, string expectedSha256)
    {
        if (!Join.KspLauncher.IsKspFolder(kspFolder))
            throw new InvalidOperationException("That isn't a KSP folder (no KSP_x64.exe).");
        if (Join.KspLauncher.IsRunning(kspFolder))
            throw new InvalidOperationException("Close KSP first.");
        if (ServerInstaller.Sha256Of(zipPath) != expectedSha256)
            throw new DownloadVerificationException("The LMP client doesn't match GitHub's checksum. Nothing was installed.");

        var gameData = Path.GetFullPath(Path.Combine(kspFolder, "GameData")) + Path.DirectorySeparatorChar;
        var lmp = Path.Combine(gameData, "LunaMultiplayer");
        var harmonyExists = Directory.Exists(Path.Combine(gameData, "000_Harmony"));
        var staging = Path.Combine(kspFolder, "LunaEvo", "staging-" + Guid.NewGuid().ToString("N")[..8]);
        string? backup = null;
        try
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                if (zip.Entries.Count > MaxEntries || zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes)
                    throw new InvalidDataException("That doesn't look like LMP's client (far too big).");
                var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    var wanted = name.StartsWith("GameData/LunaMultiplayer/", StringComparison.OrdinalIgnoreCase) ||
                                 (!harmonyExists && name.StartsWith("GameData/000_Harmony/", StringComparison.OrdinalIgnoreCase));
                    if (!wanted || name.EndsWith('/'))
                        continue;
                    var destination = Path.GetFullPath(Path.Combine(root, name));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"That zip has a file outside GameData ({entry.FullName}). Nothing was installed.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination);
                }
            }
            var newLmp = Path.Combine(staging, "GameData", "LunaMultiplayer");
            if (!Directory.Exists(Path.Combine(newLmp, "Plugins")))
                throw new InvalidDataException("That zip has no LMP client in it.");

            if (Directory.Exists(lmp))
            {
                // Keep the player's LMP data (server list, saved mod rules) in the new version.
                var data = Path.Combine(lmp, "Data");
                if (Directory.Exists(data))
                    CopyFolder(data, Path.Combine(newLmp, "Data"));
                backup = Path.Combine(kspFolder, "LunaEvo", "Backups", $"LunaMultiplayer_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
                for (var i = 2; Directory.Exists(backup); i++) // two installs within a second
                    backup = Path.Combine(kspFolder, "LunaEvo", "Backups", $"LunaMultiplayer_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{i}");
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                Directory.Move(lmp, backup);
            }
            Directory.Move(newLmp, lmp);
            var newHarmony = Path.Combine(staging, "GameData", "000_Harmony");
            if (!harmonyExists && Directory.Exists(newHarmony))
                Directory.Move(newHarmony, Path.Combine(gameData, "000_Harmony"));
            return backup;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
