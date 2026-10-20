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

        // Unpack next to the Universe first and only swap once that worked, so a damaged or hostile zip can never
        // leave a half-restored Universe behind.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var staging = UniverseFolder + ".restoring-" + suffix;
        var old = UniverseFolder + ".old-" + suffix;
        try
        {
            Extract(snapshotPath, staging);
            // Rename rather than delete-then-move: on Windows a deleted folder can linger for a moment, which would
            // make the move fail. If the swap fails, the old Universe goes back.
            if (Directory.Exists(UniverseFolder))
                Directory.Move(UniverseFolder, old);
            try
            {
                Directory.Move(staging, UniverseFolder);
            }
            catch
            {
                if (Directory.Exists(old) && !Directory.Exists(UniverseFolder))
                    Directory.Move(old, UniverseFolder);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
        try
        {
            if (Directory.Exists(old))
                Directory.Delete(old, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Restored either way; a leftover Universe.old-* folder is harmless (and the safety snapshot has it too).
        }
        return safety;
    }

    public const int MaxEntries = 200_000;
    public const long MaxUnpackedBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>Unpacks a snapshot with limits, and refuses entries that would land outside the folder.</summary>
    private static void Extract(string zipPath, string target)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > MaxEntries)
            throw new InvalidDataException($"That backup has {zip.Entries.Count:N0} files, more than a Universe ever has. Not restoring it.");
        if (zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes)
            throw new InvalidDataException("That backup would unpack to more than 8 GB. Not restoring it.");

        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        foreach (var entry in zip.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"That backup contains a file outside the Universe folder ({entry.FullName}). Not restoring it.");
            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
        }
    }

    /// <summary>Deletes the oldest snapshots so only <paramref name="keep"/> remain. 0 keeps everything.</summary>
    public void Prune(int keep)
    {
        if (keep <= 0)
            return;
        foreach (var old in List().Skip(keep))
            File.Delete(old.Path);
    }

    /// <summary>Like <see cref="Prune(int)"/>, but only among snapshots with this label, so automatic ones never push out manual ones.</summary>
    public void Prune(int keep, string label)
    {
        if (keep <= 0)
            return;
        foreach (var old in List().Where(s => s.Label == label).Skip(keep))
            File.Delete(old.Path);
    }

    /// <summary>
    /// A safety snapshot taken automatically (server stopped, before an LMP update). Keeps the newest
    /// <paramref name="keep"/> with that label. Returns null, never throws, if there's no Universe or it can't be read.
    /// </summary>
    public UniverseSnapshot? Safety(string label, int keep = 5)
    {
        if (!Directory.Exists(UniverseFolder))
            return null;
        try
        {
            var snapshot = Create(label).Snapshot;
            Prune(keep, label);
            return snapshot;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string LabelOf(string name)
    {
        // universe_yyyy-MM-dd_HH-mm-ss_<label>.zip
        var parts = Path.GetFileNameWithoutExtension(name).Split('_');
        return parts.Length >= 4 ? parts[3] : "";
    }
}
