// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/ServerInstaller.cs
// [Core] Downloads an LMP server zip and installs or updates it, never touching Config, Universe, logs or Plugins.
// Used by: SetupWizard, SettingsPage (update)
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using System.IO.Compression;

namespace LunaEvo.ConfigManager.Core.Setup;

public sealed record InstallResult(int FilesWritten, string? BinariesBackup);

public sealed class ServerInstaller(HttpClient http)
{
    /// <summary>
    /// Top-level names in a server folder that belong to the user, not to LMP's download. An update never
    /// overwrites them (LMP's zips don't contain them anyway; this makes sure of it).
    /// </summary>
    public static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "Config", "Universe", "logs", "Plugins", "LunaEvo", "LMPPlayerBans.txt", "LunaEvo.ConfigManager.exe",
    };

    /// <summary>Downloads a release asset to a temp file and returns its path.</summary>
    public async Task<string> DownloadAsync(LmpAsset asset, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var path = Path.Combine(Path.GetTempPath(), "LunaEvo", "downloads", asset.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var response = await http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(cancel);
        await using var target = File.Create(path);

        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancel)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancel);
            done += read;
            if (total > 0)
                progress?.Report(Math.Min(1, (double)done / total));
        }
        progress?.Report(1);
        return path;
    }

    /// <summary>
    /// Extracts an LMP server zip into <paramref name="serverDirectory"/>. LMP's zips put everything under an
    /// "LMPServer/" folder; that prefix is dropped. When the folder already has a server, the files about to be
    /// replaced are zipped into LunaEvo/ServerBackups first. The server must not be running.
    /// </summary>
    public static InstallResult Install(string zipPath, string serverDirectory)
    {
        Directory.CreateDirectory(serverDirectory);
        var root = Path.GetFullPath(serverDirectory) + Path.DirectorySeparatorChar;

        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries
            .Select(e => (Entry: e, Relative: StripRoot(e.FullName)))
            .Where(x => x.Relative.Length > 0 && !x.Entry.FullName.EndsWith('/') && !IsProtected(x.Relative))
            .ToList();

        string? backup = null;
        var existing = entries.Select(x => Path.Combine(serverDirectory, x.Relative)).Where(File.Exists).ToList();
        if (existing.Count > 0)
        {
            var oldVersion = LmpVersion.OfServer(serverDirectory)?.ToString() ?? "unknown";
            var folder = Directory.CreateDirectory(Path.Combine(serverDirectory, "LunaEvo", "ServerBackups")).FullName;
            backup = Path.Combine(folder, $"server-{oldVersion}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            using var archive = ZipFile.Open(backup, ZipArchiveMode.Create);
            foreach (var file in existing)
                archive.CreateEntryFromFile(file, Path.GetRelativePath(serverDirectory, file));
        }

        foreach (var (entry, relative) in entries)
        {
            var target = Path.GetFullPath(Path.Combine(serverDirectory, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe path in the zip: {entry.FullName}"); // "zip slip"
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
        return new InstallResult(entries.Count, backup);
    }

    /// <summary>True if the folder looks like an LMP server (has Server.dll).</summary>
    public static bool IsServerFolder(string directory) => File.Exists(Path.Combine(directory, "Server.dll"));

    private static string StripRoot(string name)
    {
        name = name.Replace('\\', '/');
        return name.StartsWith("LMPServer/", StringComparison.OrdinalIgnoreCase) ? name["LMPServer/".Length..] : name;
    }

    private static bool IsProtected(string relative) => Protected.Contains(relative.Split('/')[0]);
}
