// LUNA EVO · LunaEvo.ConfigManager.Core/Ops/ProfileStore.cs
// [Core] Named copies of a server's whole config ("Career campaign", "Sandbox chaos") that can be switched between.
// Used by: ProfilesPage
// Guide:   Meat and Potatoes/13-Automation.md

using LunaEvo.ConfigManager.Core.Files;

namespace LunaEvo.ConfigManager.Core.Ops;

public sealed record ConfigProfile(string Name, string Folder, DateTime Saved, int FileCount);

/// <summary>Profiles live in &lt;server&gt;/LunaEvo/Profiles/&lt;name&gt;/*.xml, next to the server they belong to.</summary>
public sealed class ProfileStore(string serverDirectory)
{
    public string Root { get; } = Path.Combine(serverDirectory, "LunaEvo", "Profiles");

    public IReadOnlyList<ConfigProfile> List() =>
        Directory.Exists(Root)
            ? new DirectoryInfo(Root).GetDirectories()
                .Select(d => new ConfigProfile(d.Name, d.FullName, d.GetFiles("*.xml").Select(f => f.LastWriteTime).DefaultIfEmpty(d.LastWriteTime).Max(), d.GetFiles("*.xml").Length))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

    /// <summary>Copies the config files as they are on disk (save first!) into a profile, replacing one with the same name.</summary>
    public ConfigProfile Save(string name, string configDirectory)
    {
        var folder = Path.Combine(Root, SafeName(name));
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.GetFiles(configDirectory, "*.xml"))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        return List().First(p => p.Name == SafeName(name));
    }

    /// <summary>Backs up the current config, then copies the profile's files over it. Returns the backup folder.</summary>
    public string Apply(string name, string configDirectory)
    {
        var folder = Path.Combine(Root, SafeName(name));
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"No profile called {name}.");
        var backup = BackupService.Backup(configDirectory, Directory.GetFiles(configDirectory, "*.xml"));
        foreach (var file in Directory.GetFiles(folder, "*.xml"))
            File.Copy(file, Path.Combine(configDirectory, Path.GetFileName(file)), overwrite: true);
        return backup;
    }

    public void Delete(string name)
    {
        var folder = Path.Combine(Root, SafeName(name));
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    /// <summary>A profile name that's safe as a folder name.</summary>
    public static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim('.', ' ');
        return safe.Length == 0 ? "Profile" : safe;
    }
}
