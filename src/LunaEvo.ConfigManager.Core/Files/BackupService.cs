// LUNA EVO · LunaEvo.ConfigManager.Core/Files/BackupService.cs
// [Core] Timestamped backups of config files before every save (Config/Backups/yyyy-MM-dd_HH-mm-ss/), and pruning of old ones.
// Used by: ConfigSession.Save
// Guide:   Meat and Potatoes/02-Core.md

namespace LunaEvo.ConfigManager.Core.Files;

public static class BackupService
{
    public const string BackupFolderName = "Backups";

    /// <summary>Copies the given files into Config/Backups/yyyy-MM-dd_HH-mm-ss/ and returns that folder.</summary>
    public static string Backup(string configDirectory, IEnumerable<string> filePaths, DateTime? now = null)
    {
        var stamp = (now ?? DateTime.Now).ToString("yyyy-MM-dd_HH-mm-ss");
        var root = Path.Combine(configDirectory, BackupFolderName);
        var folder = Path.Combine(root, stamp);
        for (var i = 2; Directory.Exists(folder); i++)
            folder = Path.Combine(root, $"{stamp}_{i}");

        Directory.CreateDirectory(folder);
        foreach (var path in filePaths.Where(File.Exists))
            File.Copy(path, Path.Combine(folder, Path.GetFileName(path)));
        return folder;
    }

    /// <summary>Deletes the oldest backup folders so only <paramref name="keep"/> remain. 0 keeps everything.</summary>
    public static void Prune(string configDirectory, int keep)
    {
        var root = new DirectoryInfo(Path.Combine(configDirectory, BackupFolderName));
        if (keep <= 0 || !root.Exists)
            return;
        // Names are timestamps, so ordinal order is age order.
        foreach (var old in root.GetDirectories().OrderByDescending(d => d.Name, StringComparer.Ordinal).Skip(keep))
            old.Delete(recursive: true);
    }
}
