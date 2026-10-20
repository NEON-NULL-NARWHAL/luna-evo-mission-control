// LUNA EVO · LunaEvo.ConfigManager.Core/AppSettings.cs
// [Core] LUNA EVO's own preferences (accent colour, theme, last server, backups to keep...) and where they're saved.
// Used by: MainWindow, SettingsPage
// Guide:   Meat and Potatoes/04-App-UI.md

using System.Text.Json;

namespace LunaEvo.ConfigManager.Core;

/// <summary>LUNA EVO's own preferences (not the server's). Stored per user in %APPDATA%\LunaEvo\ConfigManager.</summary>
public sealed record AppSettings
{
    public const string DefaultAccent = "#3FAE2A";

    /// <summary>Seed colour the whole palette is generated from, as #RRGGBB.</summary>
    public string AccentColor { get; init; } = DefaultAccent;

    /// <summary>"Dark", "Light" or "System".</summary>
    public string Theme { get; init; } = "Dark";

    public string? LastServerDirectory { get; init; }
    public bool OpenLastServer { get; init; } = true;

    /// <summary>How many backup folders to keep in Config/Backups. 0 keeps them all.</summary>
    public int BackupsToKeep { get; init; } = 20;

    /// <summary>Lines kept in the server console view.</summary>
    public int ConsoleLines { get; init; } = 5000;

    /// <summary>Seconds to wait for a clean stop before killing the server.</summary>
    public int StopTimeoutSeconds { get; init; } = 30;

    public bool ConfirmStop { get; init; } = true;
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LunaEvo", "ConfigManager", "settings.json");

    /// <summary>Returns defaults when the file is missing or unreadable, so a bad file never stops the app starting.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Writes the settings as indented JSON, creating the folder if needed.</summary>
    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Json));
    }
}
