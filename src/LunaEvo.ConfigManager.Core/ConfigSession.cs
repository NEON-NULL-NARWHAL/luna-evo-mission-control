// LUNA EVO · LunaEvo.ConfigManager.Core/ConfigSession.cs
// [Core] The heart of the editor: the schema plus all loaded config files. Get/set values, validate, track unsaved changes, save with backup.
// Used by: MainWindow, SettingRow, ListEditor, ServerPage, tests
// Guide:   Meat and Potatoes/02-Core.md

using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Core;

/// <summary>Everything the GUI edits: the schema from Server.dll plus the loaded config files.</summary>
public sealed class ConfigSession
{
    private readonly Dictionary<string, ConfigFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _originals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyFiles = new(StringComparer.OrdinalIgnoreCase);

    private ConfigSession(string serverDirectory, string configDirectory, LmpSchema schema)
    {
        ServerDirectory = serverDirectory;
        ConfigDirectory = configDirectory;
        Schema = schema;
    }

    public string ServerDirectory { get; }
    public string ConfigDirectory { get; }
    public LmpSchema Schema { get; }
    public bool IsDirty => _dirtyFiles.Count > 0;
    public IReadOnlyCollection<string> DirtyFiles => _dirtyFiles;

    /// <param name="serverDirectory">Folder containing Server.dll (the schema source).</param>
    /// <param name="configDirectory">Folder with the XML files; defaults to the server's Config folder.</param>
    public static ConfigSession Open(string serverDirectory, string? configDirectory = null)
    {
        configDirectory ??= Path.Combine(serverDirectory, "Config");
        var session = new ConfigSession(serverDirectory, configDirectory, LmpSchemaLoader.Load(serverDirectory));
        session.LoadFiles();
        return session;
    }

    /// <summary>True if the file exists in the config folder (a brand-new server may not have them all yet).</summary>
    public bool HasFile(string fileName) => _files.ContainsKey(fileName);

    /// <summary>Current value, or the default when the file is missing the element (the server would add it on startup).</summary>
    public string? GetValue(string fileName, string setting)
    {
        var schema = Schema.Find(fileName);
        var definition = schema?.Find(setting);
        if (_files.TryGetValue(fileName, out var file) && file.GetValue(setting) is { } value)
            return value;
        return definition == null ? null : schema!.DefaultFor(definition, LmpRules.PresetFor(this, fileName));
    }

    /// <summary>Items of a list setting (LMPModControl.xml), or empty.</summary>
    public IReadOnlyList<string> GetList(string fileName, string setting) =>
        _files.TryGetValue(fileName, out var file) ? file.GetList(setting) ?? [] : [];

    /// <summary>The value "reset to default" uses. For GameplaySettings this follows the GameDifficulty preset.</summary>
    public string DefaultFor(string fileName, string setting)
    {
        var schema = Schema.Find(fileName) ?? throw new ArgumentException($"Unknown file {fileName}");
        var definition = schema.Find(setting) ?? throw new ArgumentException($"Unknown setting {fileName}/{setting}");
        return schema.DefaultFor(definition, LmpRules.PresetFor(this, fileName));
    }

    /// <summary>Changes a value in memory (nothing is written until Save). Adds the element if the file lacks it.</summary>
    public void SetValue(string fileName, string setting, string value)
    {
        var file = RequireFile(fileName);
        if (file.GetValue(setting) == value.Replace("\r\n", "\n"))
            return;
        file.SetValue(setting, value, Schema.Find(fileName)?.Find(setting)?.Description);
        UpdateDirty(fileName);
    }

    /// <summary>Replaces a list setting's items in memory.</summary>
    public void SetList(string fileName, string setting, IEnumerable<string> items)
    {
        var file = RequireFile(fileName);
        var list = items.ToList();
        if (file.GetList(setting)?.SequenceEqual(list) == true)
            return;
        file.SetList(setting, list, Schema.Find(fileName)?.Find(setting)?.Description);
        UpdateDirty(fileName);
    }

    /// <summary>A file is dirty while any of its settings differs from disk, so changing a value back un-dirties it.</summary>
    private void UpdateDirty(string fileName)
    {
        var settings = Schema.Find(fileName)?.Settings ?? [];
        if (settings.Any(s => IsModified(fileName, s.Name)))
            _dirtyFiles.Add(fileName);
        else
            _dirtyFiles.Remove(fileName);
    }

    /// <summary>True if the value differs from what was on disk when the file was loaded or last saved.</summary>
    public bool IsModified(string fileName, string setting)
    {
        var current = _files.TryGetValue(fileName, out var file) ? Snapshot(file, setting) : null;
        return _originals.TryGetValue(Key(fileName, setting), out var original) ? current != original : current != null;
    }

    /// <summary>Every problem across all files: per-setting checks plus the cross-file rules in LmpRules.</summary>
    public IReadOnlyList<Issue> Validate()
    {
        var issues = new List<Issue>();
        foreach (var schema in Schema.Files.Where(f => HasFile(f.FileName)))
        foreach (var setting in schema.Settings)
        {
            var value = GetValue(schema.FileName, setting.Name);
            if (value != null && SettingValidator.Validate(setting, value) is { } error)
                issues.Add(new Issue(schema.FileName, setting.Name, Severity.Error, error));
        }
        issues.AddRange(LmpRules.Check(this));
        return issues;
    }

    /// <summary>Backs up the changed files, then writes them. Returns the backup folder, or null if nothing changed.</summary>
    /// <param name="backupsToKeep">Older backup folders beyond this many are deleted. 0 keeps them all.</param>
    public string? Save(int backupsToKeep = 0)
    {
        if (!IsDirty)
            return null;

        var changed = _dirtyFiles.Select(name => _files[name]).ToList();
        var backup = BackupService.Backup(ConfigDirectory, changed.Select(f => f.Path));
        foreach (var file in changed)
            file.Save();
        BackupService.Prune(ConfigDirectory, backupsToKeep);

        foreach (var file in changed)
            TakeSnapshot(Path.GetFileName(file.Path), file);
        _dirtyFiles.Clear();
        return backup;
    }

    private void LoadFiles()
    {
        foreach (var schema in Schema.Files)
        {
            var path = Path.Combine(ConfigDirectory, schema.FileName);
            if (!File.Exists(path))
                continue;
            var file = ConfigFile.Load(path);
            _files[schema.FileName] = file;
            TakeSnapshot(schema.FileName, file);
        }
    }

    private void TakeSnapshot(string fileName, ConfigFile file)
    {
        foreach (var setting in Schema.Find(fileName)?.Settings ?? [])
            _originals[Key(fileName, setting.Name)] = Snapshot(file, setting.Name) ?? "";
    }

    private static string? Snapshot(ConfigFile file, string setting) =>
        file.GetList(setting) is { Count: > 0 } list ? string.Join("\n", list) : file.GetValue(setting);

    private ConfigFile RequireFile(string fileName) =>
        _files.TryGetValue(fileName, out var file)
            ? file
            : throw new InvalidOperationException($"{fileName} isn't in {ConfigDirectory}. Start the server once to generate it.");

    private static string Key(string fileName, string setting) => fileName + "/" + setting;
}
