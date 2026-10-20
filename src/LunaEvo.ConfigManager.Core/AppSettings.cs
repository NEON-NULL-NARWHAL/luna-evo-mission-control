// LUNA EVO · LunaEvo.ConfigManager.Core/AppSettings.cs
// [Core] LUNA EVO's own preferences (accent colour, theme, last server, backups to keep...) and where they're saved.
// Used by: MainWindow, SettingsPage
// Guide:   Meat and Potatoes/04-App-UI.md

using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>Show a list of what changed before saving.</summary>
    public bool ReviewChangesBeforeSave { get; init; } = true;

    // Automation (Server Console)

    /// <summary>Start the server again if it exits without being asked (gives up after 3 crashes in 10 minutes).</summary>
    public bool AutoRestartOnCrash { get; init; } = true;

    /// <summary>"HH:mm" to restart every day, with a one-minute warning in chat. Empty = off.</summary>
    public string DailyRestartTime { get; init; } = "";

    /// <summary>Zip the Universe every this many hours while LUNA EVO runs the server. 0 = off.</summary>
    public int UniverseBackupHours { get; init; }

    public int UniverseBackupsToKeep { get; init; } = 10;

    // Joining (player side)

    /// <summary>Servers saved on the Join page. Passwords are never stored here (they go only into LMP's own list).</summary>
    public IReadOnlyList<SavedServer> SavedServers { get; init; } = [];

    /// <summary>KSP's folder, if it isn't in a Steam library LUNA EVO can find.</summary>
    public string? KspFolder { get; init; }

    // Updates

    /// <summary>Look for a newer LMP server on GitHub when a server is opened.</summary>
    public bool CheckForLmpUpdates { get; init; } = true;

    public bool IncludeLmpPrereleases { get; init; }
}

/// <summary>A server on the Join page's list.</summary>
public sealed record SavedServer(string Name, string Host, int Port = 8800, int WebsitePort = 8900);

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
            if (!File.Exists(path))
                return new();
            var text = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(text, Json) ?? new();
            // 0.3.0 stored a Discord webhook URL here in plain text. Discord support is gone; rewrite the file so
            // that secret doesn't linger on disk.
            if (text.Contains("\"DiscordWebhookUrl\"", StringComparison.Ordinal))
                Save(settings, path);
            return settings;
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
