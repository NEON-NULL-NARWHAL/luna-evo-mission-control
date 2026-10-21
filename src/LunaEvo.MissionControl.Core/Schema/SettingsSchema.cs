// LUNA EVO · LunaEvo.MissionControl.Core/Schema/SettingsSchema.cs
// [Core] Plain data types describing settings and files (name, type, default, description, limits, presets).
// Used by: Everything
// Guide:   Meat and Potatoes/02-Core.md

namespace LunaEvo.MissionControl.Core.Schema;

public enum SettingKind
{
    Bool,
    Integer,
    Decimal,
    Text,
    MultilineText,
    Enum,
    StringList,
    /// <summary>A list of small records, e.g. LMPModControl.xml's plugin lists (DllFile: Text, Link, FilePath, Sha).</summary>
    ObjectList,
    /// <summary>A type we can't edit yet. Shown read-only.</summary>
    Unsupported,
}

/// <summary>One setting, as described by LMP's own settings class.</summary>
public sealed record SettingDefinition(
    string Name,
    SettingKind Kind,
    string ClrTypeName,
    string Description,
    string DefaultValue,
    IReadOnlyList<string> EnumValues,
    decimal? Min,
    decimal? Max,
    int? MaxLength)
{
    /// <summary>For <see cref="SettingKind.ObjectList"/>: the XML element of each item (the record's type name, e.g. "DllFile").</summary>
    public string? ItemElement { get; init; }

    /// <summary>For <see cref="SettingKind.ObjectList"/>: the item's fields in declaration order.</summary>
    public IReadOnlyList<string> ItemFields { get; init; } = [];
}

/// <summary>One config file and the settings it holds, in declaration order.</summary>
public sealed record SettingsFileSchema(
    string FileName,
    string RootElement,
    IReadOnlyList<SettingDefinition> Settings,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Presets)
{
    public string DisplayName => FileName.Replace("Settings.xml", "").Replace(".xml", "");

    /// <summary>The setting with this element name, or null.</summary>
    public SettingDefinition? Find(string name) => Settings.FirstOrDefault(s => s.Name == name);

    /// <summary>Default for a setting, taken from a preset (e.g. GameplaySettings "Hard") when one is given and known.</summary>
    public string DefaultFor(SettingDefinition setting, string? presetName) =>
        presetName != null && Presets.TryGetValue(presetName, out var preset) && preset.TryGetValue(setting.Name, out var value)
            ? value
            : setting.DefaultValue;
}

public sealed record LmpSchema(string ServerVersion, IReadOnlyList<SettingsFileSchema> Files)
{
    /// <summary>The file with this name (case-insensitive), or null.</summary>
    public SettingsFileSchema? Find(string fileName) =>
        Files.FirstOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase));
}
