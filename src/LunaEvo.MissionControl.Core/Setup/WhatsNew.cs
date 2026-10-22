// LUNA EVO · LunaEvo.MissionControl.Core/Setup/WhatsNew.cs
// [Core] Reads CHANGELOG.md (carried inside the app) into versions for the What's new tab.
// Used by: SettingsPage (What's new tab)
// Guide:   Meat and Potatoes/18-Updating-LUNA-EVO.md

using System.Text.RegularExpressions;

namespace LunaEvo.MissionControl.Core.Setup;

/// <summary>One version's entry: "2.8.0 (2026-10-22)" and its lines, with markdown's ** and ` taken out.</summary>
public sealed record WhatsNewEntry(string Heading, IReadOnlyList<string> Lines);

public static partial class WhatsNew
{
    /// <summary>The newest <paramref name="count"/> versions ("## x.y.z ..." headings), newest first, as written.</summary>
    public static IReadOnlyList<WhatsNewEntry> Parse(string markdown, int count = 6)
    {
        var entries = new List<WhatsNewEntry>();
        string? heading = null;
        var lines = new List<string>();
        foreach (var raw in (markdown ?? "").Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                if (heading != null && entries.Count < count)
                    entries.Add(new WhatsNewEntry(heading, Trim(lines)));
                heading = Plain(raw[3..]);
                lines = [];
                continue;
            }
            if (heading == null)
                continue;
            var line = raw.TrimEnd();
            if (line.StartsWith("  ", StringComparison.Ordinal) && lines.Count > 0 && !line.TrimStart().StartsWith('-'))
                lines[^1] += " " + Plain(line.Trim()); // a wrapped bullet: one line
            else if (line.StartsWith("### ", StringComparison.Ordinal))
                lines.Add(Plain(line[4..]).ToUpperInvariant());
            else
                lines.Add(Plain(line));
        }
        if (heading != null && entries.Count < count)
            entries.Add(new WhatsNewEntry(heading, Trim(lines)));
        return entries;
    }

    private static string Plain(string text) => LinkPattern().Replace(text.Replace("**", "").Replace("`", ""), "$1").Trim();

    private static List<string> Trim(List<string> lines)
    {
        var kept = lines.Where((l, i) => l.Length > 0 || (i > 0 && lines[i - 1].Length > 0)).ToList();
        while (kept.Count > 0 && kept[^1].Length == 0)
            kept.RemoveAt(kept.Count - 1);
        return kept;
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();
}
