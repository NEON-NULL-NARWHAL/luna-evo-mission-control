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

    /// <summary>
    /// For a ckan.exe the user already had: true if it looks like a Windows program called *.exe. It's the user's own
    /// choice to run it; <see cref="IdentifyAsync"/> says whether it's an official build.
    /// </summary>
    public static bool LooksLikeExe(string path)
    {
        try
        {
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                return false;
            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The official CKAN release whose ckan.exe has exactly this file's SHA-256 (GitHub's digest), or null.</summary>
    public static async Task<string?> IdentifyAsync(HttpClient http, string exe, CancellationToken cancel = default)
    {
        var sha = await Task.Run(() => ServerInstaller.Sha256Of(exe), cancel);
        return (await AvailableAsync(http, includePrereleases: true, cancel)).FirstOrDefault(r => r.Exe.Sha256 == sha).Release?.Tag;
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
                // CKAN writes "M:/Steam/..."; show and compare it like every other Windows path.
                .Select(i => i with { Path = System.IO.Path.GetFullPath(i.Path).TrimEnd(System.IO.Path.DirectorySeparatorChar) })
                .ToList();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
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

    // Never "." or "..": Remove("..") would otherwise delete the folder above Root.
    internal static string SafeVersion(string tag) =>
        new string(tag.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray()).Trim('.') is { Length: > 0 } s ? s : "unknown";

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

/// <summary>One mod to install: CKAN's identifier, optionally an exact version.</summary>
public sealed record CkanMod(string Identifier, string? Version = null);

/// <summary>
/// A mod list to hand to CKAN: a pasted or loaded <c>.ckan</c> modpack (CKAN's "export modpack", or an install's
/// CKAN/installed-*.ckan), or plain mod identifiers one per line.
/// <para>
/// Safety: a .ckan file can carry its own <c>download</c> URL, and CKAN would fetch and install whatever is there (KSP
/// then runs any DLL in GameData). So the user's file is <b>never</b> passed to CKAN. Only identifiers (checked against
/// CKAN's identifier rules) and versions are read out and passed as <c>ckan install Mod Mod=version</c>, so CKAN
/// installs them from its own catalog. (Not a metapackage of our own: CKAN would keep it in its list, stale after
/// the next list. Checked with ckan.exe 1.36.4.)
/// </para>
/// </summary>
public static partial class CkanModList
{
    public const int MaxMods = 2000;

    public static (IReadOnlyList<CkanMod> Mods, IReadOnlyList<string> Problems) Parse(string? text)
    {
        var mods = new List<CkanMod>();
        var problems = new List<string>();
        text = text?.Trim() ?? "";
        if (text.StartsWith('{'))
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                var choices = 0;
                foreach (var list in new[] { "depends", "recommends" })
                    if (json.RootElement.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array)
                        foreach (var item in items.EnumerateArray())
                        {
                            // "any_of" (one of several mods) has no single name: say so rather than drop it quietly.
                            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("any_of", out _))
                            {
                                choices++;
                                continue;
                            }
                            var name = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                            var version = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                            Add(name, version, problems, mods);
                        }
                if (choices > 0)
                    problems.Add($"{choices} \"one of these\" choice(s) (any_of) skipped; install those in CKAN.");
                if (json.RootElement.TryGetProperty("download", out _))
                    problems.Add("This .ckan file has its own download link; it's ignored. Only its list of mods is used, from CKAN's catalog.");
            }
            catch (JsonException e)
            {
                problems.Add("That isn't a valid .ckan file: " + e.Message);
            }
        }
        else
        {
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                Add(line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries)[0], null, problems, mods);
            }
        }
        var unique = mods.DistinctBy(m => m.Identifier, StringComparer.OrdinalIgnoreCase).ToList();
        if (unique.Count > MaxMods)
            problems.Add($"Only the first {MaxMods} of {unique.Count} mods are used.");
        return (unique.Take(MaxMods).ToList(), problems);
    }

    /// <summary>
    /// The mods you chose in this install (CKAN keeps them in CKAN/installed-&lt;instance&gt;.ckan; dependencies CKAN
    /// added by itself aren't in it), and the KSP versions it says they're for. Null if CKAN hasn't written one.
    /// </summary>
    public static (IReadOnlyList<CkanMod> Mods, string? KspMin, string? KspMax)? InstalledList(string kspFolder)
    {
        try
        {
            var folder = System.IO.Path.Combine(kspFolder, "CKAN");
            var file = Directory.Exists(folder)
                ? new DirectoryInfo(folder).GetFiles("installed-*.ckan").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
                : null;
            if (file == null || file.Length > MaxListBytes)
                return null;
            var text = File.ReadAllText(file.FullName);
            var (mods, _) = Parse(text);
            using var json = JsonDocument.Parse(text);
            string? Version(string name) =>
                json.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: < 30 } s && VersionText().IsMatch(s) ? s : null;
            return (mods, Version("ksp_version_min"), Version("ksp_version_max"));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public const int MaxListBytes = 5 * 1024 * 1024;

    /// <summary>
    /// A .ckan mod list to share, written by LUNA EVO: a CKAN metapackage with only the mods (and, with
    /// <paramref name="exactVersions"/>, their versions) and the KSP range. Not a copy of CKAN's own file, which has your
    /// Windows user name as its author. CKAN can install it (File → Install from .ckan), and so can LUNA EVO.
    /// </summary>
    public static string Export(IReadOnlyList<CkanMod> mods, string name, string? kspMin = null, string? kspMax = null, bool exactVersions = true)
    {
        var id = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        var pack = new Dictionary<string, object?>
        {
            ["spec_version"] = "v1.6",
            ["identifier"] = id.Length > 0 && char.IsAsciiLetterOrDigit(id[0]) ? id : "LunaEvoModList",
            ["name"] = name,
            ["abstract"] = "Mod list exported by LUNA EVO",
            ["author"] = "LUNA EVO",
            ["version"] = DateTime.UtcNow.ToString("yyyy.MM.dd.HH.mm.ss"),
            ["license"] = "unknown",
            ["kind"] = "metapackage",
        };
        if (kspMin != null)
            pack["ksp_version_min"] = kspMin;
        if (kspMax != null)
            pack["ksp_version_max"] = kspMax;
        pack["depends"] = mods.Select(m => exactVersions && m.Version is { Length: > 0 }
            ? new Dictionary<string, string> { ["name"] = m.Identifier, ["version"] = m.Version }
            : new Dictionary<string, string> { ["name"] = m.Identifier }).ToList();
        return JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// The same install commands without --headless: CKAN then works everything out, lists what it would install and asks
    /// "Continue? [Y/n]". Answered "n" (see <see cref="CkanOutput"/>), nothing is downloaded or changed.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> CheckCommands(string kspFolder, IReadOnlyList<CkanMod> mods) =>
        InstallCommands(kspFolder, mods).Select(c => (IReadOnlyList<string>)c.Where(a => a != "--headless").ToList()).ToList();

    /// <summary>`ckan update`: refresh CKAN's catalog for this install.</summary>
    public static IReadOnlyList<string> UpdateArguments(string kspFolder) => ["update", "--headless", "--gamedir", kspFolder];

    /// <summary>
    /// `ckan install Mod Mod=version ...`, no extra recommendations, no prompts. Split into several commands when the list
    /// wouldn't fit one Windows command line (32,767 characters). Identifiers never start with "-", so none reads as an option.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> InstallCommands(string kspFolder, IReadOnlyList<CkanMod> mods, int maxLength = 24_000)
    {
        var commands = new List<IReadOnlyList<string>>();
        List<string>? current = null;
        var length = 0;
        foreach (var mod in mods)
        {
            var argument = mod.Version is { Length: > 0 } ? $"{mod.Identifier}={mod.Version}" : mod.Identifier;
            if (current == null || length + argument.Length + 1 > maxLength)
            {
                current = ["install", "--headless", "--no-recommends", "--gamedir", kspFolder];
                length = current.Sum(a => a.Length + 3);
                commands.Add(current);
            }
            current.Add(argument);
            length += argument.Length + 1;
        }
        return commands;
    }

    /// <summary>
    /// True if CKAN has this install open: CKAN/registry.locked holds the process id of the CKAN using it. A lock
    /// left behind by a CKAN that has since closed doesn't count (CKAN clears those itself).
    /// </summary>
    public static bool IsLocked(string kspFolder)
    {
        var lockFile = System.IO.Path.Combine(kspFolder, "CKAN", "registry.locked");
        if (!File.Exists(lockFile))
            return false;
        try
        {
            var text = File.ReadAllText(lockFile).Trim();
            if (!int.TryParse(text, out var pid))
                return true; // unreadable: be careful
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false; // that process is gone
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>How many mods CKAN has installed here (CKAN/registry.json installed_modules), or null.</summary>
    public static int? InstalledCount(string kspFolder)
    {
        try
        {
            var path = System.IO.Path.Combine(kspFolder, "CKAN", "registry.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 200 * 1024 * 1024)
                return null;
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream);
            return json.RootElement.TryGetProperty("installed_modules", out var m) && m.ValueKind == JsonValueKind.Object ? m.EnumerateObject().Count() : null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Add(string? name, string? version, List<string> problems, List<CkanMod> mods)
    {
        if (name == null)
            return;
        if (!Identifier().IsMatch(name))
        {
            problems.Add($"\"{(name.Length > 40 ? name[..40] + "…" : name)}\" isn't a CKAN mod identifier; skipped.");
            return;
        }
        mods.Add(new CkanMod(name, version is { Length: > 0 and < 60 } && VersionText().IsMatch(version) ? version : null));
    }

    // CKAN's identifier rule: letters, digits and "-"/"_", not starting with "-" (so it can never look like an option).
    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,99}$")]
    private static partial System.Text.RegularExpressions.Regex Identifier();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9.:+_~-]+$")]
    private static partial System.Text.RegularExpressions.Regex VersionText();
}
