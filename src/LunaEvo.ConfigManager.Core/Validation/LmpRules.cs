// LUNA EVO · LunaEvo.ConfigManager.Core/Validation/LmpRules.cs
// [Core] Rules that span settings or files: heartbeat < timeout, port clashes, greyed-out settings, difficulty presets.
// Used by: ConfigSession.Validate, SettingRow
// Guide:   Meat and Potatoes/02-Core.md

using System.Globalization;

namespace LunaEvo.ConfigManager.Core.Validation;

/// <summary>Rules that span settings or files, which the per-setting comments can't express.</summary>
public static class LmpRules
{
    public const string Connection = "ConnectionSettings.xml";
    public const string Website = "WebsiteSettings.xml";
    public const string DedicatedServer = "DedicatedServerSettings.xml";
    public const string General = "GeneralSettings.xml";
    public const string Gameplay = "GameplaySettings.xml";

    private static readonly HashSet<string> RainbowColours = ["Red", "Green", "Blue"];
    private static readonly HashSet<string> CareerOnly =
        ["StartingFunds", "StartingReputation", "FundsGainMultiplier", "RepGainMultiplier", "FundsLossMultiplier", "RepLossMultiplier", "RepLossDeclined"];
    private static readonly HashSet<string> CareerOrScience = ["StartingScience", "ScienceGainMultiplier"];

    /// <summary>Returns why a setting has no effect right now, or null if it applies.</summary>
    public static string? InactiveReason(ConfigSession session, string fileName, string setting)
    {
        if (fileName == DedicatedServer && RainbowColours.Contains(setting) && IsTrue(session.GetValue(DedicatedServer, "UseRainbowEffect")))
            return "Only used when UseRainbowEffect is off.";

        if (fileName == Gameplay)
        {
            var mode = session.GetValue(General, "GameMode");
            if (CareerOnly.Contains(setting) && mode != "Career")
                return $"Only used in Career mode (GameMode is {mode}).";
            if (CareerOrScience.Contains(setting) && mode == "Sandbox")
                return "Only used in Career and Science mode (GameMode is Sandbox).";
        }

        return null;
    }

    /// <summary>The preset whose values count as "default" for a file, e.g. GameplaySettings follows GameDifficulty.</summary>
    public static string? PresetFor(ConfigSession session, string fileName) =>
        fileName == Gameplay ? session.GetValue(General, "GameDifficulty") : null;

    /// <summary>The cross-setting checks: heartbeat vs timeout, game vs website port, and the difficulty preset warning.</summary>
    public static IEnumerable<Issue> Check(ConfigSession session)
    {
        var heartbeat = Int(session.GetValue(Connection, "HearbeatMsInterval"));
        var timeout = Int(session.GetValue(Connection, "ConnectionMsTimeout"));
        if (heartbeat is { } h && timeout is { } t && h >= t)
            yield return new Issue(Connection, "HearbeatMsInterval", Severity.Error, $"Must be lower than ConnectionMsTimeout ({t}).");

        var gamePort = Int(session.GetValue(Connection, "Port"));
        var webPort = Int(session.GetValue(Website, "Port"));
        if (IsTrue(session.GetValue(Website, "EnableWebsite")) && gamePort is { } g && g == webPort)
            yield return new Issue(Website, "Port", Severity.Warning, $"Same as the game port in ConnectionSettings ({g}).");

        // LMP compares every gameplay value against the preset and quietly switches to Custom if any differ.
        var difficulty = session.GetValue(General, "GameDifficulty");
        var gameplay = session.Schema.Find(Gameplay);
        if (gameplay != null && difficulty != null && difficulty != "Custom" && gameplay.Presets.TryGetValue(difficulty, out var preset)
            && session.HasFile(Gameplay))
        {
            var changed = preset.Where(p => !SameValue(session.GetValue(Gameplay, p.Key), p.Value)).Select(p => p.Key).ToList();
            if (changed.Count > 0)
                yield return new Issue(Gameplay, null, Severity.Warning,
                    $"{changed.Count} value(s) differ from the {difficulty} preset ({string.Join(", ", changed.Take(3))}{(changed.Count > 3 ? ", ..." : "")}). " +
                    "On startup the server will switch GameDifficulty to Custom.");
        }
    }

    private static bool IsTrue(string? value) => bool.TryParse(value, out var b) && b;

    private static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    // The server compares ToString() of parsed values, so "1" and "1.0", or "True" and "true", are equal.
    private static bool SameValue(string? a, string b)
    {
        if (a == b) return true;
        if (a == null) return false;
        if (decimal.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            decimal.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return x == y;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
