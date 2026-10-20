// LUNA EVO · LunaEvo.ConfigManager.Core/Ops/UniverseBackups.cs
// [Core] Zips the server's Universe (vessels, kerbals, scenarios...) into snapshots, and restores them.
// Used by: ProfilesPage, ServerPage (scheduled backups)
// Guide:   Meat and Potatoes/13-Automation.md

using System.IO.Compression;

namespace LunaEvo.ConfigManager.Core.Ops;

public sealed record UniverseSnapshot(string Path, string Name, DateTime Created, long Size, string Label);

/// <summary>
/// LMP keeps the whole game state in &lt;server&gt;/Universe and backs it up in place on its own interval. These are
/// separate, dated zips in &lt;server&gt;/LunaEvo/UniverseBackups that you can go back to.
/// </summary>
public sealed class UniverseBackups(string serverDirectory)
{
    public string UniverseFolder { get; } = Path.Combine(serverDirectory, "Universe");
    public string Root { get; } = Path.Combine(serverDirectory, "LunaEvo", "UniverseBackups");

    public IReadOnlyList<UniverseSnapshot> List() =>
        Directory.Exists(Root)
            ? new DirectoryInfo(Root).GetFiles("universe_*.zip")
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .Select(f => new UniverseSnapshot(f.FullName, f.Name, f.LastWriteTime, f.Length, LabelOf(f.Name)))
                .ToList()
            : [];

    /// <summary>
    /// Zips the Universe folder. Files are read with sharing on, so this works while the server runs (send LMP
    /// "/backup now" first so its files are fresh). Unreadable files are skipped and counted.
    /// </summary>
    public (UniverseSnapshot Snapshot, int Skipped) Create(string label = "manual", DateTime? now = null)
    {
        if (!Directory.Exists(UniverseFolder))
            throw new DirectoryNotFoundException($"No Universe folder at {UniverseFolder}. Start the server once to create it.");
        Directory.CreateDirectory(Root);

        var stamp = (now ?? DateTime.Now).ToString("yyyy-MM-dd_HH-mm-ss");
        var safeLabel = new string(label.Where(char.IsLetterOrDigit).ToArray());
        var path = Path.Combine(Root, $"universe_{stamp}_{(safeLabel.Length > 0 ? safeLabel : "manual")}.zip");
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(Root, $"universe_{stamp}_{safeLabel}_{i}.zip");

        var skipped = 0;
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            // Don't follow symlinks/junctions out of (or around in loops inside) the Universe folder.
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var file in Directory.EnumerateFiles(UniverseFolder, "*", options))
            {
                try
                {
                    using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var entry = zip.CreateEntry(Path.GetRelativePath(UniverseFolder, file).Replace('\\', '/'), CompressionLevel.Optimal);
                    using var target = entry.Open();
                    source.CopyTo(target);
                }
                catch (IOException)
                {
                    skipped++;
                }
            }
        }
        var info = new FileInfo(path);
        return (new UniverseSnapshot(path, info.Name, info.LastWriteTime, info.Length, LabelOf(info.Name)), skipped);
    }

    /// <summary>
    /// Replaces the Universe with a snapshot. The current Universe is snapshotted first ("before-restore"), so a
    /// restore can always be undone. The server must be stopped.
    /// </summary>
    /// <returns>The safety snapshot, or null if there was no Universe to save.</returns>
    public UniverseSnapshot? Restore(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException("That backup no longer exists.", snapshotPath);

        var safety = Directory.Exists(UniverseFolder) ? Create("beforerestore").Snapshot : null;
        if (Directory.Exists(UniverseFolder))
            Directory.Delete(UniverseFolder, recursive: true);
        ZipFile.ExtractToDirectory(snapshotPath, UniverseFolder);
        return safety;
    }

    /// <summary>Deletes the oldest snapshots so only <paramref name="keep"/> remain. 0 keeps everything.</summary>
    public void Prune(int keep)
    {
        if (keep <= 0)
            return;
        foreach (var old in List().Skip(keep))
            File.Delete(old.Path);
    }

    private static string LabelOf(string name)
    {
        // universe_yyyy-MM-dd_HH-mm-ss_<label>.zip
        var parts = Path.GetFileNameWithoutExtension(name).Split('_');
        return parts.Length >= 4 ? parts[3] : "";
    }
}
