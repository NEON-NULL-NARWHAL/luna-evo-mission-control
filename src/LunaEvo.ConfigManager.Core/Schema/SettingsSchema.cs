// LUNA EVO · LunaEvo.ConfigManager.Core/Schema/SettingsSchema.cs
// [Core] Plain data types describing settings and files (name, type, default, description, limits, presets).
// Used by: Everything
// Guide:   Meat and Potatoes/02-Core.md

namespace LunaEvo.ConfigManager.Core.Schema;

public enum SettingKind
{
    Bool,
    Integer,
    Decimal,
    Text,
    MultilineText,
    Enum,
    StringList,
    /// <summary>A type we can't edit yet (e.g. lists of objects in LMPModControl.xml). Shown read-only.</summary>
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
    int? MaxLength);

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
