// LUNA EVO · LunaEvo.ConfigManager.Core/Validation/SettingValidator.cs
// [Core] Checks one value against its type and limits; defines Issue and Severity.
// Used by: ConfigSession.Validate
// Guide:   Meat and Potatoes/02-Core.md

using System.Globalization;
using LunaEvo.ConfigManager.Core.Schema;

namespace LunaEvo.ConfigManager.Core.Validation;

public enum Severity { Warning, Error }

/// <summary>A problem with a setting, or with a whole file when <see cref="Setting"/> is null.</summary>
public sealed record Issue(string FileName, string? Setting, Severity Severity, string Message);

public static class SettingValidator
{
    /// <summary>Checks one value against its type and the limits found in its comment. Returns null when valid.</summary>
    public static string? Validate(SettingDefinition setting, string value)
    {
        switch (setting.Kind)
        {
            case SettingKind.Bool:
                return bool.TryParse(value, out _) ? null : "Must be true or false.";

            case SettingKind.Integer:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                    return "Must be a whole number.";
                return CheckRange(setting, whole);

            case SettingKind.Decimal:
                if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return "Must be a number.";
                return CheckRange(setting, number);

            case SettingKind.Text:
            case SettingKind.MultilineText:
                return setting.MaxLength is { } max && value.Length > max
                    ? $"Too long: {value.Length} characters, max {max}."
                    : null;

            case SettingKind.Enum:
                // XmlSerializer is case-sensitive, so "sandbox" would stop the server loading the file.
                return setting.EnumValues.Contains(value, StringComparer.Ordinal)
                    ? null
                    : $"Must be one of: {string.Join(", ", setting.EnumValues)}.";

            default:
                return null;
        }
    }

    private static string? CheckRange(SettingDefinition setting, decimal value)
    {
        if (setting.Min is { } min && value < min) return $"Minimum is {min.ToString(CultureInfo.InvariantCulture)}.";
        if (setting.Max is { } max && value > max) return $"Maximum is {max.ToString(CultureInfo.InvariantCulture)}.";
        return null;
    }
}
